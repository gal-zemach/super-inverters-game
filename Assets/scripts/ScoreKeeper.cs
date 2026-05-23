
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is needed as we want to keep the score even though the scene is reset but not keep the whole GameState.
/// </summary>
public class ScoreKeeper : MonoBehaviour
{
	private static ScoreKeeper _instance;
		
	private Dictionary<string, int> _scores;
	[HideInInspector] public int DOESNT_EXIST = -1;
	
	public static ScoreKeeper getInstance { get { return _instance; } }
	
	private void Awake()
	{
		if (_instance == null)
		{
			_instance = this;
		}
		else
		{
			Destroy(this.gameObject);
			return;
		}
		
		DontDestroyOnLoad(gameObject);
		_scores = new Dictionary<string, int>();
	}

	public bool scoresExist()
	{
		return _scores.Count > 0;
	}

	public void setScore(string playerName, int score)
	{
		if (_scores.ContainsKey(playerName))
		{
			_scores[playerName] = score;
			return;
		}
		_scores.Add(playerName, score);
	}

	public int getScore(string playerName)
	{
		if (_scores.ContainsKey(playerName)) return _scores[playerName];
		return DOESNT_EXIST;
	}
	
	public void incrementScore(string playerName)
	{
		if (_scores.ContainsKey(playerName)) _scores[playerName]++;
	}
	
	public void decreaseScore(string playerName)
	{
		if (_scores.ContainsKey(playerName)) _scores[playerName]--;
	}

	public void clearScores()
	{
		_scores.Clear();
	}
}
