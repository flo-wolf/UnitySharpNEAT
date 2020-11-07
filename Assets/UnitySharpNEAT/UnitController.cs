/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using SharpNeat.Phenomes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySharpNEAT
{
    /// <summary>
    /// Abstract representation of a Unit, which is equipped with a Neural Net (IBlackBox).
    /// The IBlackBox gets fed with inputs and computes an output, which can be used to control the Unit.
    /// </summary>
    public abstract class UnitController : MonoBehaviour
    {
        public IBlackBox BlackBox { get; private set; }

        public bool IsActive
        {
            get => _isActive;
            protected set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    HandleIsActiveChanged(value);
                }
            }
        }

        private bool _isActive;


        protected virtual void FixedUpdate()
        {
            if (IsActive)
            {
                // feed the black box with input
                UpdateBlackBoxInputs(BlackBox.InputSignalArray);

                // calculate the outputs
                BlackBox.Activate();

                // do something with those outputs
                UseBlackBoxOutpts(BlackBox.OutputSignalArray);
            }
        }

        /// <summary>
        /// Called when a generation is spawned and the evolution begins. 
        /// The IBlackBox is the new Neural Net this Unit has been assigned.
        /// </summary>
        public virtual void ActivateUnit(IBlackBox blackBox)
        {
            BlackBox = blackBox;
            IsActive = true;
        }

        /// <summary>
        /// Called when the evolution stops or a generation is finished. 
        /// </summary>
        public virtual void DeactivateUnit()
        {
            BlackBox = null;
            IsActive = false;
        }

        /// <summary>
        /// Feed the BlackBox with inputs.
        /// Do that by modifying its input signal array.
        /// The size of the array corresponds to NeatSupervisor.NetworkInputCount
        /// </summary>
        protected abstract void UpdateBlackBoxInputs(ISignalArray inputSignalArray);

        /// <summary>
        /// Do something with the computed outputs of the BlackBox.
        /// The size of the array corresponds to NeatSupervisor.NetworkOutputCount
        /// </summary>
        protected abstract void UseBlackBoxOutpts(ISignalArray outputSignalArray);

        /// <summary>
        /// Called during the evaluation phase (at the end of each trail). 
        /// The performance of this unit, i.e. it's fitness, is retrieved by this function.
        /// Implement a meaningful fitness function here.
        /// </summary>
        public abstract float GetFitness();

        /// <summary>
        /// Called whenever the value of IsActive has changed.
        /// Since NeatSupervisor.cs is making use of Object Pooling, this Unit will never get destroyed. 
        /// Make sure that when IsActive gets set to false, the variables and the Transform of this Unit are reset!
        /// Consider to also disable MeshRenderers until IsActive turns true again.
        /// </summary>
        protected abstract void HandleIsActiveChanged(bool newIsActive);
    }
}
