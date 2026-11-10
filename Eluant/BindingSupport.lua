-- Eluant bindings
-- Restored from cdhowie/Eluant commit 01fe17386ddefbe8c9531466435c18f90881e90b.
-- Copyright (c) 2013 Chris Howie. MIT license; see THIRD_PARTY_NOTICES.md.

-- Make copies before cartridge code can alter globals.
local real_pcall = pcall
local real_select = select
local real_error = error

-- Avoid Lua longjmp across CLR frames: managed wrappers return success flags.
local function process_managed_call_result(flag, flag2, ...)
    if not flag then real_error(flag2, ...) end
    if not flag2 then real_error(...) end
    return ...
end

function eluant_create_managed_call_wrapper(fn)
    return function(...)
        return process_managed_call_result(real_pcall(fn, ...))
    end
end
