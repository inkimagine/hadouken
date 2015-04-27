#include <Hadouken/Scripting/Modules/BitTorrent/Events/FileCompletedEvent.hpp>

#include <Hadouken/BitTorrent/TorrentHandle.hpp>
#include "../../../duktape.h"

using namespace Hadouken::BitTorrent;
using namespace Hadouken::Scripting::Modules::BitTorrent::Events;

FileCompletedEvent::FileCompletedEvent(std::shared_ptr<TorrentHandle> handle, int index)
    : TorrentEvent(handle)
{
    index_ = index;
}

void FileCompletedEvent::push(duk_context* ctx)
{
    duk_idx_t idx = duk_push_object(ctx);
    
    TorrentEvent::push(ctx);
    duk_put_prop_string(ctx, idx, "torrent");

    duk_push_int(ctx, index_);
    duk_put_prop_string(ctx, idx, "index");
}
