
function log(text, verbose)
    if verbose then
        print(text)
    end
end

function getHeadIndex(layer, frameIndex)
    local cel = layer:cel(frameIndex)

    if cel == nil then
        return -1
    end

    local image = cel.image
    return 1 + cel.position.x + cel.position.y * 16
    -- return cel.position
end

function get_pos(layer, frameIndex)
    local cel = layer:cel(frameIndex)

    if cel == nil then
        return nil
    end

    local image = cel.image
    return cel.position
end

