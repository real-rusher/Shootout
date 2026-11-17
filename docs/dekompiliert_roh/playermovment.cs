using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class playermovment : NetworkBehaviour
{
	[SerializeField]
	private float movementSpeed = 5f;

	[SerializeField]
	private float jumpSpeed = 5f;

	[SerializeField]
	private float prejumpTime = 0.1f;

	[SerializeField]
	private float mass = 1f;

	[SerializeField]
	private float groundAcceleration = 20f;

	[SerializeField]
	private float airAccelerationAnteil = 0.5f;

	private float prejumpTimer = -1f;

	internal float movementSpeedMulitplier;

	private bool wasGrounded;

	private CharacterController controller;

	internal Vector3 velocity;

	private PlayerInput playerInput;

	private InputAction moveAction;

	private InputAction jumpAction;

	public bool isGrounded => controller.isGrounded;

	public bool _isDead => Playermanager.ownerPlayer.isDead.Value;

	public event Action OnBeforeMove;

	public event Action<bool> OnGroundStateChange;

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		moveAction = playerInput.actions["Move"];
		jumpAction = playerInput.actions["Jump"];
		controller = GetComponent<CharacterController>();
	}

	private void Update()
	{
		if (base.IsOwner && !_isDead)
		{
			UpdateMovement();
			UpdatePrejump();
			if (Manager.Instance.gamestate == Manager.GameState.Game)
			{
				UpdateJump();
			}
		}
		if (!_isDead)
		{
			UpdateGravity();
		}
	}

	private void UpdatePrejump()
	{
		if (!isGrounded)
		{
			if (jumpAction.IsPressed() && Manager.Instance.gamestate == Manager.GameState.Game)
			{
				prejumpTimer = 0f;
			}
			else if (prejumpTimer >= 0f)
			{
				prejumpTimer += Time.deltaTime;
			}
		}
		else if (prejumpTimer >= 0f && prejumpTimer <= prejumpTime && !jumpAction.IsPressed())
		{
			velocity.y = jumpSpeed;
			prejumpTimer = -1f;
		}
	}

	private void UpdateJump()
	{
		if (jumpAction.IsPressed() && isGrounded)
		{
			velocity.y = jumpSpeed;
		}
	}

	private void UpdateGround()
	{
		if (wasGrounded != isGrounded)
		{
			this.OnGroundStateChange?.Invoke(isGrounded);
			wasGrounded = isGrounded;
		}
	}

	private void UpdateGravity()
	{
		Vector3 vector = Physics.gravity * mass * Time.deltaTime;
		velocity.y = (controller.isGrounded ? velocity.y : (velocity.y + vector.y));
		controller.Move(velocity * Time.deltaTime);
	}

	private Vector3 GetMovementInput()
	{
		Vector2 vector = moveAction.ReadValue<Vector2>();
		return Vector3.ClampMagnitude(default(Vector3) + base.transform.forward * vector.y + base.transform.right * vector.x, 1f) * movementSpeed;
	}

	private void UpdateMovement()
	{
		Vector3 vector = Vector3.zero;
		if (Manager.Instance.gamestate == Manager.GameState.Game)
		{
			vector = GetMovementInput();
		}
		float t = Time.deltaTime * groundAcceleration * (isGrounded ? 1f : airAccelerationAnteil);
		velocity.x = Mathf.Lerp(velocity.x, vector.x, t);
		velocity.z = Mathf.Lerp(velocity.z, vector.z, t);
	}

	internal override string __getTypeName()
	{
		return "playermovment";
	}
}
