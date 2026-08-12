using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace GIF
{
    public class EventSignalLogger : MonoBehaviour
    {
        public bool _logInputEvents = false;
        public bool _logActivityEvents = false;
        public bool _logInputModifierEvents = false;
        public bool _logPointerEvents = false;
        public bool _logDirectionalEvents = false;
        public bool _logHotkeyEvents = false;




        // Input Logs
        //pointer
        public void LogPointerActivity()
        {
            if (_logInputEvents && _logPointerEvents && _logActivityEvents)
                Debug.Log("Pointer activity detected");
        }
        public void LogPointerPositionChange(Vector3 newPosition)
        {
            if (_logInputEvents && _logPointerEvents)
                Debug.Log($"Pointer position change detected: {newPosition}");
        }
        public void LogPointerScroll(Vector2 detectedScroll)
        {
            if (_logInputEvents && _logPointerEvents)
                Debug.Log($"Pointer croll detected: {detectedScroll}");
        }
        public void LogLclick()
        {
            if (_logInputEvents && _logPointerEvents)
                Debug.Log($"Pointer Leftclick detected");
        }
        public void LogRclick()
        {
            if (_logInputEvents && _logPointerEvents)
                Debug.Log($"Pointer Rightclick detected");
        }
        public void LogMclick()
        {
            if (_logInputEvents && _logPointerEvents)
                Debug.Log($"Pointer Middleclick detected");
        }

        //directional
        public void LogDirectionalActivity()
        {
            if (_logInputEvents && _logDirectionalEvents && _logActivityEvents)
                Debug.Log("Directional activity detected");
        }
        public void LogDirectionalInput(Vector2 input)
        {
            if (_logInputEvents && _logDirectionalEvents)
                Debug.Log($"Directional input detected: {input}");
        }
        public void LogLrotate()
        {
            if (_logInputEvents && _logDirectionalEvents)
                Debug.Log("Directional lRotate detected");
        }
        public void LogRrotate()
        {
            if (_logInputEvents && _logDirectionalEvents)
                Debug.Log("Directional rRotate detected");
        }
        public void LogConfirm()
        {
            if (_logInputEvents && _logDirectionalEvents)
                Debug.Log("Directional confirm detected");
        }
        public void LogBack()
        {
            if (_logInputEvents && _logDirectionalEvents)
                Debug.Log("Directional back detected");
        }

        //hotkeys
        public void LogGridJumpHotkey()
        {
            if (_logInputEvents && _logHotkeyEvents)
                Debug.Log("Hotkey jumpToOtherGrid detected");
        }
        public void LogInventoryHotkey()
        {
            if (_logInputEvents && _logHotkeyEvents)
                Debug.Log("Hotkey toggleInventory detected");
        }
        public void LogEditInputFieldHotkey()
        {
            if (_logInputEvents && _logHotkeyEvents)
                Debug.Log("Hotkey editInputField detected");
        }

        //input modifiers
        public void LogAlphaModifierStatus(bool currentStatus)
        {
            if (_logInputEvents && _logInputModifierEvents)
                Debug.Log($"Alpha modifier status: {currentStatus}");
        }
        public void LogBetaModifierStatus(bool currentStatus)
        {
            if (_logInputEvents && _logInputModifierEvents)
                Debug.Log($"Beta modifier status: {currentStatus}");
        }
        public void LogGammaModifierStatus(bool currentStatus)
        {
            if (_logInputEvents && _logInputModifierEvents)
                Debug.Log($"Gamma modifier status: {currentStatus}");
        }

        //custom logs
        public void LogCustomMessage(string message)
        {
            Debug.Log($"{message}");
        }


    }
}
