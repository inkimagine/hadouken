﻿using System;
using System.Collections.Generic;
using Hadouken.Framework.Rpc;
using Hadouken.Plugins.Torrents.Dto;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace Hadouken.Plugins.Torrents.BitTorrent
{
    public sealed class MonoTorrentEngine : IBitTorrentEngine
    {
        private static readonly IDictionary<Tuple<TorrentState, TorrentState>, string> EventMap =
            new Dictionary<Tuple<TorrentState, TorrentState>, string>();

        private readonly IEngineSettingsFactory _settingsFactory;
        private readonly IJsonRpcClient _rpcClient;
        private readonly object _managersLock = new object();
        private readonly IDictionary<string, IExtendedTorrentManager> _managers =
            new Dictionary<string, IExtendedTorrentManager>(StringComparer.InvariantCultureIgnoreCase); 
 
        private ClientEngine _engine;

        static MonoTorrentEngine()
        {
            EventMap.Add(Tuple.Create(TorrentState.Downloading, TorrentState.Seeding), "torrent.completed");
            EventMap.Add(Tuple.Create(TorrentState.Stopped, TorrentState.Downloading), "torrent.started");
            EventMap.Add(Tuple.Create(TorrentState.Stopped, TorrentState.Seeding), "torrent.started");
            EventMap.Add(Tuple.Create(TorrentState.Hashing, TorrentState.Downloading), "torrent.started");
            EventMap.Add(Tuple.Create(TorrentState.Hashing, TorrentState.Seeding), "torrent.started");
        }

        public MonoTorrentEngine(IEngineSettingsFactory settingsFactory, IJsonRpcClient rpcClient)
        {
            _settingsFactory = settingsFactory;
            _rpcClient = rpcClient;
        }

        public void Load()
        {
            LoadEngine();
            LoadManagers();
        }

        private void LoadManagers()
        {
        }

        private void LoadEngine()
        {
            var settings = _settingsFactory.Build();
            _engine = new ClientEngine(settings);
        }

        public void Unload()
        {
            UnloadManagers();
            _engine.Dispose();
        }

        private void UnloadManagers()
        {
            foreach (var manager in Managers)
            {
                _engine.Unregister(manager.Manager);
            }
        }

        public IEnumerable<IExtendedTorrentManager> Managers
        {
            get { lock (_managersLock) return _managers.Values; }
        }

        public IExtendedTorrentManager Get(string infoHash)
        {
            lock (_managersLock)
            {
                if (_managers.ContainsKey(infoHash))
                    return _managers[infoHash];
            }

            return null;
        }

        public IExtendedTorrentManager Add(byte[] data, string savePath = null, string label = null)
        {
            Torrent torrent;

            if (!Torrent.TryLoad(data, out torrent))
                return null;

            if (String.IsNullOrEmpty(savePath))
                savePath = _engine.Settings.SavePath;

            var manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            IExtendedTorrentManager extendedManager = new ExtendedTorrentManager(manager);
            extendedManager.Manager.TorrentStateChanged += Manager_TorrentStateChanged;

            if (Get(extendedManager.FriendlyInfoHash) != null)
                return null;

            _engine.Register(manager);

            lock (_managersLock)
            {
                _managers.Add(extendedManager.FriendlyInfoHash, extendedManager);
            }

            _rpcClient.SendEventAsync("torrent.added", new TorrentOverview(extendedManager.Manager));

            return extendedManager;
        }

        private void Manager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            var tuple = Tuple.Create(e.OldState, e.NewState);

            if (EventMap.ContainsKey(tuple))
            {
                var eventName = EventMap[tuple];
                _rpcClient.SendEventAsync(eventName, new TorrentOverview(e.TorrentManager));

                return;
            }

            if (e.NewState == TorrentState.Error)
            {
                _rpcClient.SendEventAsync("torrent.error", new TorrentOverview(e.TorrentManager));
            }
            else if (e.NewState == TorrentState.Stopped)
            {
                _rpcClient.SendEventAsync("torrent.stopped", new TorrentOverview(e.TorrentManager));
            }
            else if (e.NewState == TorrentState.Paused)
            {
                _rpcClient.SendEventAsync("torrent.paused", new TorrentOverview(e.TorrentManager));
            }
        }

        public void Remove(IExtendedTorrentManager manager)
        {
            throw new NotImplementedException();
        }
    }
}