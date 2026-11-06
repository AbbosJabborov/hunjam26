using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shift.Player
{
    /// <summary>
    /// Reads player input through Unity Input System with robust fallback support.
    /// Exposes clean polling properties and events.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Action Asset (Optional - fallbacks to direct device polling)")]
        [SerializeField] private InputActionAsset inputActions;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SwitchTriggered { get; private set; }
        public bool ShootTriggered { get; private set; }
        public bool ShootHeld { get; private set; }
        public bool SlideHeld { get; private set; }
        public bool GrappleTriggered { get; private set; }
        public bool GrappleHeld { get; private set; }
        public bool RestartTriggered { get; private set; }

        public event Action OnJumpPressed;
        public event Action OnSwitchDimensionPressed;
        public event Action OnShootPressed;
        public event Action OnGrapplePressed;
        public event Action OnGrappleReleased;
        public event Action OnRestartPressed;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _switchAction;
        private InputAction _shootAction;
        private InputAction _crouchAction;

        private void Awake()
        {
            SetupActions();
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void SetupActions()
        {
            if (inputActions != null)
            {
                var playerMap = inputActions.FindActionMap("Player");
                if (playerMap != null)
                {
                    _moveAction = playerMap.FindAction("Move");
                    _lookAction = playerMap.FindAction("Look");
                    _jumpAction = playerMap.FindAction("Jump");
                    _shootAction = playerMap.FindAction("Attack");
                    _crouchAction = playerMap.FindAction("Crouch");
                    // Try to find SwitchDimension action or create one
                    _switchAction = playerMap.FindAction("SwitchDimension");
                }
            }

            // If Switch action wasn't in the asset, create a fallback action for Left Shift
            if (_switchAction == null)
            {
                _switchAction = new InputAction("SwitchDimension", InputActionType.Button);
                _switchAction.AddBinding("<Keyboard>/leftShift");
                _switchAction.AddBinding("<Gamepad>/rightShoulder");
            }
        }

        private void EnableActions()
        {
            _moveAction?.Enable();
            _lookAction?.Enable();
            _jumpAction?.Enable();
            _shootAction?.Enable();
            _crouchAction?.Enable();
            _switchAction?.Enable();
        }

        private void DisableActions()
        {
            _moveAction?.Disable();
            _lookAction?.Disable();
            _jumpAction?.Disable();
            _shootAction?.Disable();
            _crouchAction?.Disable();
            _switchAction?.Disable();
        }

        private void Update()
        {
            ReadInputs();
        }

        private void ReadInputs()
        {
            // Reset frame triggers
            JumpTriggered = false;
            SwitchTriggered = false;
            ShootTriggered = false;
            GrappleTriggered = false;
            RestartTriggered = false;

            // --- Movement Vector ---
            if (_moveAction != null && _moveAction.enabled)
            {
                MoveInput = _moveAction.ReadValue<Vector2>();
            }
            else
            {
                Vector2 move = Vector2.zero;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                }
                MoveInput = move.normalized;
            }

            // --- Look Vector ---
            if (_lookAction != null && _lookAction.enabled)
            {
                LookInput = _lookAction.ReadValue<Vector2>();
            }
            else
            {
                var mouse = Mouse.current;
                LookInput = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            }

            // --- Jump ---
            if (_jumpAction != null && _jumpAction.enabled)
            {
                if (_jumpAction.WasPressedThisFrame())
                {
                    JumpTriggered = true;
                    OnJumpPressed?.Invoke();
                }
                JumpHeld = _jumpAction.IsPressed();
            }
            else
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame)
                    {
                        JumpTriggered = true;
                        OnJumpPressed?.Invoke();
                    }
                    JumpHeld = kb.spaceKey.isPressed;
                }
            }

            // --- Dimension Switch ---
            if (_switchAction != null && _switchAction.enabled)
            {
                if (_switchAction.WasPressedThisFrame())
                {
                    SwitchTriggered = true;
                    OnSwitchDimensionPressed?.Invoke();
                }
            }
            else
            {
                var kb = Keyboard.current;
                if (kb != null && kb.leftShiftKey.wasPressedThisFrame)
                {
                    SwitchTriggered = true;
                    OnSwitchDimensionPressed?.Invoke();
                }
            }

            // --- Shoot ---
            if (_shootAction != null && _shootAction.enabled)
            {
                if (_shootAction.WasPressedThisFrame())
                {
                    ShootTriggered = true;
                    OnShootPressed?.Invoke();
                }
                ShootHeld = _shootAction.IsPressed();
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        ShootTriggered = true;
                        OnShootPressed?.Invoke();
                    }
                    ShootHeld = mouse.leftButton.isPressed;
                }
            }

            // --- Slide / Crouch ---
            if (_crouchAction != null && _crouchAction.enabled)
            {
                SlideHeld = _crouchAction.IsPressed();
            }
            else
            {
                var kb = Keyboard.current;
                SlideHeld = kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
            }

            // --- Grapple (E key) ---
            var kbDevice = Keyboard.current;
            if (kbDevice != null)
            {
                if (kbDevice.eKey.wasPressedThisFrame)
                {
                    GrappleTriggered = true;
                    OnGrapplePressed?.Invoke();
                }
                if (kbDevice.eKey.wasReleasedThisFrame)
                {
                    OnGrappleReleased?.Invoke();
                }
                GrappleHeld = kbDevice.eKey.isPressed;
            }

            // --- Restart ---
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                RestartTriggered = true;
                OnRestartPressed?.Invoke();
            }
        }
    }
}

