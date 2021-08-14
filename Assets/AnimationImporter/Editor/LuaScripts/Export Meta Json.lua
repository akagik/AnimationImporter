print("start")
dofile("./common.lua")

local function execute()
    print("execute2")
    
    local target_parts = {}
    target_parts[1] = "head"
    
    local layer_info = {}
    
    local sprite_filename = app.params["filename"]
    local output_filepath = app.params["o"]
    
    for i, part_name in ipairs(target_parts) do
        layer_info[part_name] = {}
        layer_info[part_name]["index_layer_name"] = part_name .. "-index"
        layer_info[part_name]["index_layer"] = nil
        
        layer_info[part_name]["pos_layer_name"] = part_name .. "-pos"
        layer_info[part_name]["pos_layer"] = nil
        
        layer_info[part_name]["flip_layer_name"] = part_name .. "-flip"
        layer_info[part_name]["flip_layer"] = nil
    end
    
    print("sprite_filename: " .. sprite_filename)
    
    local sprite = app.open(sprite_filename)
    local found = false

    -- Search layers
    for i, layer in ipairs(sprite.layers) do
        for key, info in pairs(layer_info) do
            if layer.name == info["index_layer_name"] then
                info["index_layer"] = layer
            end
            if layer.name == info["pos_layer_name"] then
                info["pos_layer"] = layer
            end
            if layer.name == info["flip_layer_name"] then
                info["flip_layer"] = layer
            end
        end
    end

    f = io.open(output_filepath, "w")
    
    local json = "";
    json = json .. "{\n"
    json = json .. "  \"frames\": [\n"

    for i, frame in ipairs(sprite.frames) do
        json = json .. "    {\n"
        for key, info in pairs(layer_info) do
            json = json .. "      \"" .. key .. "\": {\n"
            
            if info["index_layer"] ~= nil then
                local index = getHeadIndex(info["index_layer"], i)
                local unityIndex = index;
                
                if unityIndex ~= -1 then
                    unityIndex = unityIndex - 1
                end
                
                json = json .. "        \"index\":" .. tostring(unityIndex) .. ",\n"
            end
            
            if info["pos_layer"] ~= nil then
                local pos = get_pos(info["pos_layer"], i)
                
                if (pos ~= nil) then
                    json = json .. "        \"pos\":{ \"x\": " .. tostring(pos.x) .. ", \"y\": " .. tostring(pos.y) .. "},\n"
                else
                    json = json .. "        \"pos\":null,\n"
                end
            end
            
            if json:sub(-2) == ",\n" then
                json = json:sub(0, -3) .. "\n"
            end
            
            json = json .. "      }\n"
        end
        
        json = json .. "    },\n"
        
        ::continue::
    end
    
    if json:sub(-2) == ",\n" then
        json = json:sub(0, -3) .. "\n"
    end
    
    -- 最後にf:closeでファイルを閉じる
    json = json .. "  ]\n"
    json = json .. "}\n"
    
    f:write(json)
    f:close()
end


execute()