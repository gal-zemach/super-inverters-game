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

#if UNITY_EDITOR
	// Sound-dropout probe: who is actually receiving PlayShoot calls. Read by the
	// debug panel to distinguish "not called at all" / "called on a stale instance" /
	// "called but the Shoot source is destroyed".
	public static int DebugShootCalls;
	public static int DebugLastShootFrame = -1;
	public static int DebugLastShootSfxId;
#endif

	public void PlayShoot()
	{
#if UNITY_EDITOR
		DebugShootCalls++;
		DebugLastShootFrame = Time.frameCount;
		DebugLastShootSfxId = GetInstanceID();
		if (Shoot == null)
			Debug.LogWarning($"[AudioDebug] PlayShoot on '{transform.root.name}' (sfx id {GetInstanceID()}): " +
			                 "Shoot source is null/destroyed — call silently swallowed.");
#endif
		if (Shoot == null) return;
		Shoot.volume = shootVolume;
		Shoot.Play();
#if UNITY_EDITOR
		// Sound-dropout probe (Editor only): voice virtualization is decided by the mixer
		// after Play(), so check one frame later. A virtual voice "plays" silently —
		// exactly the reported symptom (state looks healthy, no sound).
		StartCoroutine(DebugCheckShootAudible());
#endif
	}

#if UNITY_EDITOR
	private System.Collections.IEnumerator DebugCheckShootAudible()
	{
		yield return null;
		if (Shoot == null) yield break;
		if (Shoot.isPlaying && !Shoot.isVirtual) yield break;

		int playing = 0, virtualCount = 0;
		foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
		{
			if (!s.isPlaying) continue;
			playing++;
			if (s.isVirtual) virtualCount++;
		}
		Debug.LogWarning(
			$"[AudioDebug] Shoot SFX inaudible on {transform.root.name}: " +
			$"isPlaying={Shoot.isPlaying} isVirtual={Shoot.isVirtual} vol={Shoot.volume:F2} | " +
			$"scene voices: {playing} playing, {virtualCount} virtual | " +
			$"AudioListener.pause={AudioListener.pause}");
	}
#endif

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
