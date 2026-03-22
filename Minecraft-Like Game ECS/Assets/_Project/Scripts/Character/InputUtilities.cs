using System;
using UnityEngine;

public struct FixedInputEvent
{
    byte m_WasEverSet;
    uint m_LastSetTick;

    public void Set(uint tick)
    {
        m_LastSetTick = tick;
        m_WasEverSet = 1;
    }

    public bool IsSet(uint tick)
    {
        if (m_WasEverSet == 1)
        {
            return tick == m_LastSetTick;
        }

        return false;
    }
}
