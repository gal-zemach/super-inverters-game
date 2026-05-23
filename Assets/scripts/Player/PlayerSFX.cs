using Game;
using UnityEngine;

public class PlayerSFX : MonoBehaviour
{
	public AudioSource Jump, DoubleJump, Shoot, Impact, Death;

	[SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for the shoot sound.")]
	private float shootVolume = 0.35f;

	public void PlayJump()
	{
		Jump.Play();
	}

	public void PlayDoubleJump()
	{
		DoubleJump.Play();
	}

	public void PlayShoot()
	{
		if (Shoot == null) return;
		Shoot.volume = shootVolume;
		Shoot.Play();
	}

	public void PlayImpact()
	{
		Impact.Play();
	}

	public void PlayDeath()
	{
		Death.Play();
	}
}
