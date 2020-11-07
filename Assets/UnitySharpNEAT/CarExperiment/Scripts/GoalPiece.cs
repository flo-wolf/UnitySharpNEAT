/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using UnityEngine;
using System.Collections;

namespace UnitySharpNEAT
{
	public class GoalPiece : MonoBehaviour
	{
		void OnCollisionEnter(Collision col)
		{
			if (col.collider.CompareTag("Car"))
			{
				col.collider.GetComponent<CarController>().NewLap();
			}
		}
	}
}