using UnityEngine;

// 키보드 입력 모음. (Unity의 새 입력 방식과 예전 방식 모두에서 동작)
//   숫자 1~4 (키보드 위쪽 숫자줄) : 전투 배속
//   스페이스 바                  : 전투 일시정지
//   왼쪽 Ctrl (누르고 있는 동안)  : 대사 빨리 감기
public static class GameInput
{
    // 이번 프레임에 누른 배속 숫자 (1~4). 안 눌렀으면 0
    public static int SpeedKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return 0;
        if (kb.digit1Key.wasPressedThisFrame) return 1;
        if (kb.digit2Key.wasPressedThisFrame) return 2;
        if (kb.digit3Key.wasPressedThisFrame) return 3;
        if (kb.digit4Key.wasPressedThisFrame) return 4;
        return 0;
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        return 0;
#endif
    }

    public static bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    public static bool FastForwardHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.leftCtrlKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftControl);
#endif
    }
}
