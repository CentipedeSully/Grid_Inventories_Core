
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace dtsInventory
{
    public class InputDriver : MonoBehaviour
    {

        //Declarations
        [Header("Input Settings")]
        [SerializeField] private float _pointerClickDelay = .1f;
        [SerializeField] private float _directionalMoveDelay = 0.13f;
        private bool _isCoolingDownPointer = false;
        private bool _isCoolingDownDirectional = false;

        //pointer inputs
        private bool _pointerActivityDetected = false;
        private bool _lClick = false;
        private bool _rClick = false;
        private bool _mClick = false;
        private Vector2 _scrollDelta = Vector2.zero;
        private Vector3 _pointerPosition = Vector3.negativeInfinity;
        private Vector3 _LastPointerPosition = Vector3.negativeInfinity;


        //directional inputs
        private bool _directionalActivityDetected = false;
        private bool _leftCmd = false;
        private bool _rightCmd = false;
        private bool _upCmd = false;
        private bool _downCmd = false;
        private Vector2 _directionalInput = Vector2.zero;

        private bool _lRotate = false;
        private bool _lRotateHold = false;
        private bool _rRotate = false;
        private bool _rRotateHold = false;
        private bool _confirm = false;
        private bool _back = false;

        //hotkey inputs
        private bool _inventoryCmd = false;
        private bool _editInputFieldCmd = false;
        private bool _jumpWindow = false;

        //input modifier inputs
        private bool _alphaInput = false;
        private bool _betaInput = false;
        private bool _gammaInput = false;



        [Header("Unity Events: Pointer")]
        public UnityEvent OnPointerActivityDetected;
        public UnityEvent<Vector3> OnPointerPositionChanged;
        public UnityEvent<Vector2> OnPointerScroll;
        public UnityEvent OnLclick;
        public UnityEvent OnRclick;
        public UnityEvent OnMclick;

        [Header("Unity Events: Directional")]
        public UnityEvent OnDirectionalActivityDetected;
        public UnityEvent<Vector2> OnDirectionalInput;
        public UnityEvent OnLeftAction;
        public UnityEvent OnRightAction;
        public UnityEvent OnConfirm;
        public UnityEvent OnBack;

        [Header("Unity Events: Special Hotkeys")]
        public UnityEvent OnJumpElementsHotkey;
        public UnityEvent OnEditHotkey;
        public UnityEvent OnInventoryHotkey;

        [Header("Unity Events: Input Modifiers")]
        public UnityEvent<bool> OnAlphaInputModifierRead;
        public UnityEvent<bool> OnBetaInputModifierRead;
        public UnityEvent<bool> OnGammaInputModifierRead;



        //monobehaviour
        private void Update()
        {
            ListenForPointerInput();
            ListenForKeyboardCommands();

            RaiseRelevantUnityEvents();
        }



        //internals
        private void ListenForPointerInput()
        {
            if (Input.mousePresent)
            {
                //Track last Mouse Position
                _LastPointerPosition = _pointerPosition;
                _pointerPosition = Input.mousePosition;


                _lClick = Input.GetMouseButtonDown(0);
                _rClick = Input.GetMouseButtonDown(1);
                _mClick = Input.GetMouseButtonDown(2);
                _scrollDelta = Input.mouseScrollDelta;

                //update our pointer activity status
                if (_LastPointerPosition != _pointerPosition || _lClick || _rClick || _mClick || _scrollDelta != Vector2.zero)
                    _pointerActivityDetected = true;
                else
                    _pointerActivityDetected = false;
            }

            //update the pointer activity status if the mouse is lost
            else if (_pointerActivityDetected == true)
                _pointerActivityDetected = false;
        }

        private void ListenForKeyboardCommands()
        {
            _leftCmd = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            _rightCmd = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            _upCmd = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            _downCmd = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

            _lRotate = Input.GetKeyDown(KeyCode.Q);
            _lRotateHold = Input.GetKey(KeyCode.Q);
            _rRotate = Input.GetKeyDown(KeyCode.E);
            _rRotateHold = Input.GetKey(KeyCode.E);

            _confirm = Input.GetKeyDown(KeyCode.Return);
            _back = Input.GetKeyDown(KeyCode.Escape);
            _jumpWindow = Input.GetKeyDown(KeyCode.Tab);
            _inventoryCmd = Input.GetKeyDown(KeyCode.I);
            _editInputFieldCmd = Input.GetKeyDown(KeyCode.R);
            _alphaInput = Input.GetKey(KeyCode.LeftShift);
            _betaInput = Input.GetKey(KeyCode.LeftControl);
            _gammaInput = Input.GetKey(KeyCode.LeftAlt);

            //only read directional input to determine if keyboard is active
            //modifier/hotkeys could be used with the pointer, so don't change input mode for those
            if (_leftCmd || _rightCmd || _upCmd || _downCmd || _jumpWindow)
                _directionalActivityDetected = true;
            else _directionalActivityDetected = false;

            if (_leftCmd || _rightCmd || _upCmd || _downCmd)
            {
                _directionalInput = Vector2.zero;
                if (_leftCmd)
                    _directionalInput.x -= 1;
                if (_rightCmd)
                    _directionalInput.x += 1;
                if (_upCmd)
                    _directionalInput.y += 1;
                if (_downCmd)
                    _directionalInput.y -= 1;
            }
            else _directionalInput = Vector2.zero;
        }

        private void RaiseRelevantUnityEvents()
        {
            //Input modifiers before ANY other inputs
            OnAlphaInputModifierRead?.Invoke(_alphaInput);
            OnBetaInputModifierRead?.Invoke(_betaInput);
            OnGammaInputModifierRead?.Invoke(_gammaInput);



            //pointer inputs
            if (_pointerActivityDetected)
                OnPointerActivityDetected?.Invoke();

            if (_pointerPosition != _LastPointerPosition)
                OnPointerPositionChanged?.Invoke(_pointerPosition);

            if (_scrollDelta != Vector2.zero)
                OnPointerScroll?.Invoke(_scrollDelta);



            if (_lClick & !_isCoolingDownPointer)
            {
                OnLclick?.Invoke();
                StartPointerCooldown();
            }
            if (_rClick & !_isCoolingDownPointer)
            {
                OnRclick?.Invoke();
                StartPointerCooldown();
            }
            if (_mClick & !_isCoolingDownPointer)
            {
                OnMclick?.Invoke();
                StartPointerCooldown();
            }



            //Directional inputs last
            if (_directionalActivityDetected)
                OnDirectionalActivityDetected?.Invoke();

            if (_directionalInput != Vector2.zero && !_isCoolingDownDirectional)
            {
                OnDirectionalInput?.Invoke(_directionalInput);
                StartDirectionalCooldown();
            }


            if (_lRotate)
                OnLeftAction?.Invoke();
            if (_rRotate)
                OnRightAction?.Invoke();


            if (_confirm)
                OnConfirm?.Invoke();
            if (_back)
                OnBack?.Invoke();


            if (_jumpWindow)
                OnJumpElementsHotkey?.Invoke();

            if (_editInputFieldCmd)
                OnEditHotkey?.Invoke();

            if (_inventoryCmd)
                OnInventoryHotkey?.Invoke();

        }

        private void StartPointerCooldown()
        {
            _isCoolingDownPointer = true;
            Invoke(nameof(EndPointerCooldown), _pointerClickDelay);
        }
        private void EndPointerCooldown()
        {
            _isCoolingDownPointer = false;
        }
        private void StartDirectionalCooldown()
        {
            _isCoolingDownDirectional = true;
            Invoke(nameof(EndDirectionalCooldown), _directionalMoveDelay);
        }
        private void EndDirectionalCooldown()
        {
            _isCoolingDownDirectional = false;
        }
    }
}

