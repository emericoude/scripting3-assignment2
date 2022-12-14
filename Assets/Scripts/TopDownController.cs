using System.Collections;
using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// Handles the player character controls.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class TopDownController : Unit
{
    #region PARAMETERS

    [Header("References")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Rigidbody _rb;
    [Tooltip("The collider attached to the rigidbody.\n\nIt is used to calculated ground check size and position.")]
    [SerializeField] private Collider _mainCollider;
    [Tooltip("The current move component. A move component handles how the unit moves, while the controller focuses on sending information (inputs) to it.")]
    [SerializeField] private MoveComponent _moveComponent;
    private PlayerInputs _inputs;
    public PlayerInputs Inputs { get { return _inputs; } }

    [Header("Parameters")]
    [Tooltip("The player's base movement speed.")]
    [SerializeField] private float _baseMoveSpeed = 30f;
    public float BaseMoveSpeed { get { return _baseMoveSpeed; } }

    [Header("Interaction System")]
    [Tooltip("The layers that can be hit by the interaction's raycast.")]
    [SerializeField] private LayerMask _interactionLayer = ~0;
    [Tooltip("The point of origin of the interaction's raycast.")]
    [SerializeField] private Transform _interactionCastPoint;
    [Tooltip("The maximum length of the interaction raycast.")]
    [SerializeField] private float _interactionLength = 1f;
    [SerializeField] private float _interactionRadius = 0.5f;
    [Tooltip("The position at which items are held.")]
    [SerializeField] private Transform _heldItemPosition;
    public Transform HeldItemPosition { get { return _heldItemPosition; } }
    public PlayerTool HeldTool { get; set; }
    private Interactable _currentInteractable;

    [Header("Debug")]
    [SerializeField] private bool _debug;

    #endregion
    #region Awake/Start | Update/FixedUpdate

    protected override void Awake()
    {
        base.Awake();

        _mainCamera ??= Camera.main;

        _rb ??= GetComponent<Rigidbody>();

        _mainCollider ??= GetComponent<Collider>();

        _inputs = new PlayerInputs();
        _inputs.Enable();
    }

    private void Update()
    {
        Interact();
    }

    private void FixedUpdate()
    {
        Movement(Time.fixedDeltaTime);
    }

    #endregion
    #region Movement

    /// <summary>
    /// Sends information to its <see cref="MoveComponent"/> (such as inputs) in order to move the rigidbody.
    /// </summary>
    /// <param name="deltaTime">The delta time to use.</param>
    private void Movement(float deltaTime)
    {
        _moveComponent.Rotate(_rb, AimRotation());
        _moveComponent.Gravity(_rb, deltaTime);
        _moveComponent.Move(
            _rb,
            _baseMoveSpeed,
            MoveDirection(),
            _moveComponent.IsGrounded(_mainCollider),
            deltaTime
        );
    }

    private Vector3 MoveDirection()
    {
        Vector2 inputDirection = _inputs.Player.Move.ReadValue<Vector2>();
        if (inputDirection == Vector2.zero)
            return Vector3.zero;

        float targetMoveAngle = ( Mathf.Atan2(inputDirection.x, inputDirection.y) * Mathf.Rad2Deg ) + _mainCamera.transform.eulerAngles.y;
        if (_moveComponent.IsGrounded(out RaycastHit hit, _mainCollider))
        {
            Vector3 moveDirectionRight = Quaternion.Euler(0f, targetMoveAngle, 0f) * Vector3.right;
            return Vector3.Cross(moveDirectionRight, hit.normal);
        }
        else
        {
            return Quaternion.Euler(0f, targetMoveAngle, 0f) * Vector3.forward;
        }
    }

    private float AimRotation()
    {
        Vector2 playerPositionToScreen = _mainCamera.WorldToScreenPoint(transform.position);
        Vector2 aimDirection = ( _inputs.Player.Aim.ReadValue<Vector2>() - playerPositionToScreen ).normalized;
        return ( Mathf.Atan2(aimDirection.x, aimDirection.y) * Mathf.Rad2Deg ) + _mainCamera.transform.eulerAngles.y;
    }

    #endregion
    #region Interact

    ///<summary>Manages the interaction functionality.</summary>
    private void Interact()
    {
        //Send a raycast forward
        if (Physics.SphereCast(_interactionCastPoint.position, _interactionRadius, _interactionCastPoint.forward, out RaycastHit hit, _interactionLength))
        {

            //Check if the layer is the interaction layer
            if (_interactionLayer == ( _interactionLayer | ( 1 << hit.transform.gameObject.layer ) ))
            {

                //Try to get the interactable component
                if (hit.transform.TryGetComponent(out Interactable interactable))
                {
                    if (interactable != _currentInteractable)
                    {
                        ResetCurrentInteractable();

                        _currentInteractable = interactable;
                    }

                    if (interactable.ShouldHold())
                    {
                        if (_inputs.Player.Interact.inProgress)
                        {
                            interactable.AddProgress(this);
                        }
                        else if (_inputs.Player.Interact.WasReleasedThisFrame())
                        {
                            ResetCurrentInteractable();
                        }
                        else
                        {
                            interactable.ShowTooltips(this);
                        }
                    }
                    else
                    {
                        if (_inputs.Player.Interact.WasPerformedThisFrame())
                        {
                            interactable.Use(this);
                        }
                        else
                        {
                            interactable.ShowTooltips(this);
                        }
                    }
                }
                else
                { //if it doesn't have an interactable component
                    //Debug.LogWarning($"{hit.transform.gameObject}: This object is on the interaction layer but it does not have an Interactable component.");
                    ResetCurrentInteractable();
                }
            }
            else
            { //if the hit is not on the interactable layer.
                ResetCurrentInteractable();
            }
        }
        else
        { //if there was no hit from the raycast
            ResetCurrentInteractable();
        }
    }

    private void ResetCurrentInteractable()
    {
        UIManager.Instance.SetInteractionText("");

        if (_currentInteractable != null)
        {
            _currentInteractable.Reset();
            _currentInteractable = null;
        }
    }

#if UNITY_EDITOR

    ///<summary>Draws interaction-related gizmos. Should be called in OnDrawGizmos().</summary>
    private void InteractionGizmos()
    {
        Gizmos.color = Color.red;

        if (Physics.Raycast(_interactionCastPoint.position, _interactionCastPoint.forward, out RaycastHit hit, _interactionLength))
        {
            if (_interactionLayer == ( _interactionLayer | ( 1 << hit.transform.gameObject.layer ) ))
            {
                Gizmos.color = Color.green;
            }

            Gizmos.DrawLine(_interactionCastPoint.position, hit.point);
        }
        else
        {
            Gizmos.DrawLine(_interactionCastPoint.position, _interactionCastPoint.position + ( _interactionCastPoint.forward * _interactionLength ));
        }
    }

#endif

    #endregion
    #region DEBUG (Gizmos)

    private void OnDrawGizmos()
    {
        if (_debug)
        {
            _moveComponent.IsGroundedGizmos(_mainCollider, true, true, true);

            Gizmos.matrix = _interactionCastPoint.localToWorldMatrix;

            //INteraction gizmos
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(Vector3.zero, _interactionRadius);
            Gizmos.DrawWireSphere(( Vector3.forward * _interactionLength ) + ( Vector3.forward * _interactionRadius ), _interactionRadius);

            var cubeSize = new Vector3(
                x: _interactionRadius * 2,
                y: _interactionRadius * 2,
                z: _interactionLength
            );

            Gizmos.DrawWireCube(( Vector3.forward * _interactionLength / 2 ) + ( Vector3.forward * _interactionRadius ), cubeSize);
        }
    }

    #endregion
}
