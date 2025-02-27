﻿using UnityEngine;
using System;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 10.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 10.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;


        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // // If there is an input and the camera position is not fixed
            // if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            // {
            //     // Don't multiply mouse input by Time.deltaTime;
            //     float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            //     // Handle mouse input for camera rotation
            //     _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
            //     _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            // }
            // else 
            // {
            //     float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            //     // Rotate camera to the right
            //     if (Input.GetKey(KeyCode.L)) 
            //     {
            //         _cinemachineTargetYaw += 1 * deltaTimeMultiplier;
            //     }
            //     // Rotate camera to the left
            //     if (Input.GetKey(KeyCode.J)) 
            //     {
            //         _cinemachineTargetYaw -= 1 * deltaTimeMultiplier;
            //     }
            //     // Rotate camera up
            //     if (Input.GetKey(KeyCode.I)) 
            //     {
            //         _cinemachineTargetPitch -= 1 * deltaTimeMultiplier;
            //     }
            //     // Rotate camera down
            //     if (Input.GetKey(KeyCode.M)) 
            //     {
            //         _cinemachineTargetPitch += 1 * deltaTimeMultiplier;
            //     }
            // }

            // float deltaTimeMultiplier = Time.deltaTime;
            float deltaTimeMultiplier = 0.25f;
            float c_yaw = 0.0f;
            float c_pitch = 0.0f;

            // Update camera rotation based on TCP data
            if (TCPManager.Instance != null && TCPManager.Instance.SensorData.TryGetValue("YAW", out string yaw) &&
                TCPManager.Instance.SensorData.TryGetValue("ENCODER_DIR", out string cam_pitch))
            {
                float yaw_deg = float.Parse(yaw);
                float cam_pdeg = float.Parse(cam_pitch);

                if (Math.Abs(yaw_deg) > 20 && Math.Abs(yaw_deg) < 90)
                {
                    c_yaw = ((yaw_deg) > 0)? 1.0f : -1.0f;
                }

                else if (cam_pdeg == 1.0f)
                {
                    c_pitch = -1.0f;
                }

                else if (cam_pdeg == -1.0f)
                {
                    c_pitch = 1.0f;
                }
        

                // if (yaw_deg > 20 && yaw_deg <90)
                // {
                //     c_yaw = 1.0f;
                //     gyroYValue = 0.0f;
                // }
                // if (yaw_deg < -20 && yaw_deg > -90)
                // {
                //     c_yaw = -1.0f;
                //     gyroYValue = 0.0f;
                // }

                // else{
                //     c_yaw = 0.0f;
                //     c_pitch = 0.0f;
                // }

                _cinemachineTargetYaw += c_yaw * deltaTimeMultiplier;
                _cinemachineTargetPitch += c_pitch * deltaTimeMultiplier;
            }

            // Clamp our rotations so our values are limited to 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            float accelXValue = 0.0f; // Declare local variables
            float accelYValue = 0.0f;
            // Set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            Vector3 inputDirection = Vector3.zero;

            // Fetch accelerometer data for movement control
            if (TCPManager.Instance != null &&
                TCPManager.Instance.SensorData.TryGetValue("PITCH", out string pitch) &&
                TCPManager.Instance.SensorData.TryGetValue("ROLL", out string roll))
            {
                float pitch_deg = float.Parse(pitch);
                float roll_deg = float.Parse(roll);
        
                // Debug.Log($" pitch_deg: {pitch_deg}");
                if (Math.Abs(pitch_deg) > 40 && Math.Abs(pitch_deg) < 60)
                {

                    accelYValue = ((pitch_deg) < 0)? -1.0f : 1.0f;
                    targetSpeed = SprintSpeed;
                    // if ((pitch_deg) <= 0){
                    //     accelXValue = 0.0f;
                    //     accelYValue = -1.0f;
                    // }

                    // else{
                    //     accelXValue = 0.0f;
                    //     accelYValue = 1.0f;

                    // }
                    
                    // // targetSpeed = 10.335f;
                    // targetSpeed = SprintSpeed;

                }

                else if (Math.Abs(pitch_deg) > 20)
                {
                    accelYValue = ((pitch_deg) < 0)? -0.5f : 0.5f;
                    targetSpeed = MoveSpeed;
                    // if ((pitch_deg) <= 0){
                    //     accelXValue = 0.0f;
                    //     accelYValue = -0.5f;
                    // }

                    // else{
                    //     accelXValue = 0.0f;
                    //     accelYValue = 0.5f;
                    // }
                    // targetSpeed = MoveSpeed;

                }
                if (Math.Abs(roll_deg) > 40 && Math.Abs(roll_deg) < 60)
                {

                    accelXValue = ((roll_deg) < 0)? -1.0f : 1.0f;
                    targetSpeed = SprintSpeed;

                }
                else if (Math.Abs(roll_deg) > 20)
                {
                    accelXValue = ((roll_deg) < 0)? -0.5f : 0.5f;
                    targetSpeed = MoveSpeed;

                }
                // else{
                //     accelXValue = 0.0f;
                //     accelYValue = 0.0f;
                // }
                // float accelXValue = float.Parse(accelX);
                // float accelYValue = float.Parse(accelY);
                // Debug.Log($" accel x: {accelXValue}");
                // Debug.Log($" accel y: {accelYValue}");
                

                // with respect to the hand gesture - pitch and roll

                inputDirection = new Vector3(accelXValue, 0.0f, accelYValue).normalized;
                // inputDirection = new Vector3(
                //     Mathf.Abs(accelXValue) > _threshold ? accelXValue : 0.0f,
                //     0.0f,
                //     Mathf.Abs(accelZValue) > _threshold ? accelZValue : 0.0f
                // ).normalized;
            }

            // If there is no accelerometer input, stop movement
            if (inputDirection == Vector3.zero) targetSpeed = 0.0f;

            // A simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // Note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // If there is no input, set the target speed to 0
            // if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // A reference to the player's current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            //float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
            float inputMagnitude = inputDirection.magnitude;

            // Accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // Creates curved result rather than a linear one giving a more organic speed change
                // Note T in Lerp is clamped, so we don't need to clamp our speed
                // _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    // Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // Round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Normalize input direction
            //Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;


            // Placeholder for ESP32 accelerometer input
            // Students should add accelerometer input logic here to update inputDirection
            // Example:
            // if (accelerometerInputDetected) {
            //     inputDirection = new Vector3(accelX, 0.0f, accelY).normalized;
            // }

            // Note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // If there is a move input, rotate player when the player is moving
            if (inputDirection != Vector3.zero)
            {

                // desired rotation angle for the robot to face the direction indicated by the inputDirection vector
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                _mainCamera.transform.eulerAngles.y;
                // Smoothly transitions the robot's current rotation to the _targetRotation
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // Rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            // Represents the final direction vector in which the robot will move
            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            // Move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                            new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            //Debug.Log($"Target Direction: {targetDirection}");

            // Update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }


        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (TCPManager.Instance != null &&
                TCPManager.Instance.SensorData.TryGetValue("BUTTON", out string j_button))

                {
                    if (j_button == "TRUE" && _jumpTimeoutDelta <= 0.0f)
                    {
                        // the square root of H * -2 * G = how much velocity needed to reach desired height
                        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                        // Reset jump timeout to prevent repeated jumps
                        _jumpTimeoutDelta = JumpTimeout;

                        // update animator if using character
                        if (_hasAnimator)
                        {
                            _animator.SetBool(_animIDJump, true);
                        }
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}