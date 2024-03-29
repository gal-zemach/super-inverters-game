﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utils.Utils {
	public static class GameUtils {
		public static T FindComponentInChildWithTag<T>(this GameObject parent, string tag)where T:Component{
			Transform t = parent.transform;
			foreach(Transform tr in t)
			{
				
				if(tr.tag == tag)
				{
					return tr.GetComponent<T>();
				}
			}
			return null;
		}
	}
}
