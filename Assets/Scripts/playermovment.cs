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

	// Original las hier immer den Zustand des eigenen Spielers, auch auf fremden Objekten.
	public bool _isDead => playermanager.isDead.Value;

	private Playermanager playermanager;

	private BotSteuerung bot;

	// Wie schnell sich die Figur gerade bewegt, 0 bis 1 (fuer die Streuung der Waffen).
	public float laufAnteil => new Vector2(velocity.x, velocity.z).magnitude / Mathf.Max(0.01f, movementSpeed);

	public event Action OnBeforeMove;

	public event Action<bool> OnGroundStateChange;

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		moveAction = playerInput.actions["Move"];
		jumpAction = playerInput.actions["Jump"];
		controller = GetComponent<CharacterController>();
	}

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		bot = GetComponent<BotSteuerung>();
	}

	// Steuerbar ist die Figur, wenn man im Spiel ist (nicht im Pausemenue). Bots immer.
	private bool steuerbar => bot != null || Manager.Instance.gamestate == Manager.GameState.Game;

	private void Update()
	{
		// Nur der Besitzer bewegt die Figur (Bots gehoeren dem Host). Fremde Figuren setzt
		// NetworkTransform. Im Original lief die Schwerkraft auch auf fremden Figuren und
		// arbeitete gegen die Synchronisation.
		if (!base.IsOwner || _isDead)
		{
			return;
		}
		// Unter die Map gefallen (Himmelsinseln): neu erscheinen, ohne Tod.
		KartenRegeln regeln = KartenRegeln.Aktuelle;
		if (regeln != null && base.transform.position.y < regeln.absturzHoehe)
		{
			velocity = Vector3.zero;
			freierFlug = false;
			playermanager.spawn();
			return;
		}
		UpdateMovement();
		UpdatePrejump();
		if (steuerbar)
		{
			UpdateJump();
		}
		UpdateGravity();
	}

	// Nach einem Sprungfeld bis zur Landung ohne Luftsteuerung: Die wuerde den Schwung in
	// gut einer Sekunde fast aufzehren, und die Flugbahn waere nicht vorhersagbar.
	private bool freierFlug;

	// Fuer Sprungfelder: setzt die Geschwindigkeit, bis zur Landung fliegt man frei.
	public void schleudern(Vector3 geschwindigkeit)
	{
		velocity = geschwindigkeit;
		freierFlug = true;
		// Nicht im selben Bild wieder auf den Boden druecken.
		controller.Move(Vector3.up * 0.1f);
	}

	private bool springenGedrueckt()
	{
		return bot != null ? bot.springen : jumpAction.IsPressed();
	}

	private void UpdatePrejump()
	{
		if (!isGrounded)
		{
			if (springenGedrueckt() && steuerbar)
			{
				prejumpTimer = 0f;
			}
			else if (prejumpTimer >= 0f)
			{
				prejumpTimer += Time.deltaTime;
			}
		}
		else if (prejumpTimer >= 0f && prejumpTimer <= prejumpTime && !springenGedrueckt())
		{
			velocity.y = jumpSpeed;
			prejumpTimer = -1f;
		}
	}

	private void UpdateJump()
	{
		if (springenGedrueckt() && isGrounded)
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
		Vector2 vector = bot != null ? Vector2.ClampMagnitude(bot.bewegung, 1f) : moveAction.ReadValue<Vector2>();
		return Vector3.ClampMagnitude(default(Vector3) + base.transform.forward * vector.y + base.transform.right * vector.x, 1f) * movementSpeed;
	}

	private void UpdateMovement()
	{
		if (freierFlug)
		{
			if (!isGrounded || velocity.y > 0f)
			{
				return;
			}
			freierFlug = false;
		}
		Vector3 vector = Vector3.zero;
		if (steuerbar)
		{
			vector = GetMovementInput();
		}
		float t = Time.deltaTime * groundAcceleration * (isGrounded ? 1f : airAccelerationAnteil);
		velocity.x = Mathf.Lerp(velocity.x, vector.x, t);
		velocity.z = Mathf.Lerp(velocity.z, vector.z, t);
	}

}
