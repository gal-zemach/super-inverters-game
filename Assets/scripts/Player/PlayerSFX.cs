using Game;
using UnityEngine;

public class PlayerSFX : MonoBehaviour
{
	public AudioSource Jump, DoubleJump, Shoot, Impact, Death;

	[SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for the shoot sound.")]
	private float shootVolume = 0.22f;

	[SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for the shot-hit impact sound.")]
	private float impactVolume = 0.2f;

	[SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for the death sound.")]
	private float deathVolume = 0.4f;

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
		if (Impact == null) return;
		Impact.volume = impactVolume;
		Impact.Play();
	}

	public void PlayDeath()
	{
		if (Death == null) return;
		var clip = Death.clip;
		if (clip == null) return;
		// Detached 2D one-shot: survives same-frame respawn/destroy on master,
		// and matches the prefab Death source (spatialBlend 0) unlike PlayClipAtPoint.
		PlayDetached2D(clip, deathVolume);
	}

	static void PlayDetached2D(AudioClip clip, float volume)
	{
		var go = new GameObject("DeathSFX");
		var src = go.AddComponent<AudioSource>();
		src.clip = clip;
		src.volume = volume;
		src.spatialBlend = 0f;
		src.Play();
		Object.Destroy(go, clip.length + 0.05f);
	}
}
