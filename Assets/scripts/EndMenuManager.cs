using System;
using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;
using UnityEngine.UI;

public class EndMenuManager : MonoBehaviour
{

	public Button[] buttons;
	private int selectedButtonIndex;

	private GameManager gameManager;
	private Animator titleAnimator;
	private float ANALOG_MOVE_THRESHOLD = 0.3f;
	private bool canInteract = true;

	void Awake()
	{
		gameManager = GameObject.Find("Game").GetComponent<GameManager>();
		
		Transform title = transform.Find("Panel").Find("Title");
		if (title != null)
			titleAnimator = title.GetComponent<Animator>();
		
		selectedButtonIndex = 0;
	}

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		CheckAxis("J1_PS4_LeftStick_Vertical");
		CheckAxis("J2_PS4_LeftStick_Vertical");

		CheckButton("J1_XBOX_X");
		CheckButton("J2_XBOX_X");
	}

	private void CheckAxis(String axis)
	{
		if (!canInteract)
			return;
		
		var axisValue = Input.GetAxis(axis);
		if (Mathf.Abs(axisValue) > ANALOG_MOVE_THRESHOLD)
		{
			canInteract = false;
			StartCoroutine(MenuChange(axisValue));
		}
	}
	
	private void CheckButton(String button)
	{
		if (Input.GetButtonDown(button))
		{
			buttons[selectedButtonIndex].onClick.Invoke();
			if (GameManager.LocalPauseActive)
			{
				gameManager.RestartMusic();
				gameManager.TogglePauseMenu();
			}
		}
	}

	IEnumerator MenuChange(float axisValue)
	{		
		selectedButtonIndex = Mod(selectedButtonIndex + (int) Mathf.Sign(axisValue), buttons.Length);
		buttons[selectedButtonIndex].Select();  
		
		yield return new WaitForSecondsRealtime(0.1f);
		canInteract = true;   // After the wait is over, the player can interact with the menu again.
	}

	public void setAnimation(int playerId) {
		if (titleAnimator == null)
			return;
		
		titleAnimator.SetInteger("winId", playerId);
	}

	int Mod(int x, int m) 
	{
		int r = x % m;
		return r < 0 ? r + m : r;
	}
}
