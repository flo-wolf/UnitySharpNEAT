/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;
using System.Collections.Generic;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System;
using System.Xml;
using System.IO;
using System.Linq;
using SharpNeat.Core;

namespace UnitySharpNEAT
{
    /// <summary>
    /// This class acts as the entry point for the NEAT evolution.
    /// It manages the UnitController's being evolved and handles the creation of the NeatEvolutionAlgorithm.
    /// It is also responsible for managing the lifecycle of the evolution, e.g. by starting/stopping it.
    /// </summary>
    public class NeatSupervisor : MonoBehaviour
    {
        #region FIELDS
        [Header("Experiment Settings")]

        [SerializeField]
        private string _experimentConfigFileName = "experiment.config";

        [SerializeField]
        private int _networkInputCount = 5;

        [SerializeField]
        private int _networkOutputCount = 2;


        [Header("Evaluation Settings")]

        [Tooltip("How many times per generation the generation gets evaluated.")]
        public int Trials = 1;

        [Tooltip("How many seconds pass between each evaluation (the duration gets scaled by the global timescale).")]
        public float TrialDuration = 20;

        [Tooltip("Stop the simulation as soon as a Unit reaches this fitness level.")]
        public float StoppingFitness = 15;


        [Header("Unit Management")]

        [SerializeField, Tooltip("The Unit Prefab, which inherits from UnitController, that should be evaluated and spawned.")]
        private UnitController _unitControllerPrefab = default;

        [SerializeField, Tooltip("The parent transform which will hold the instantiated Units.")]
        private Transform _spawnParent = default;


        [Header("Debug")]

        [SerializeField]
        private bool _enableDebugLogging = false;


        // Object pooling and Unit management
        private Dictionary<IBlackBox, UnitController> _blackBoxMap = new Dictionary<IBlackBox, UnitController>();

        private HashSet<UnitController> _unusedUnitsPool = new HashSet<UnitController>();

        private HashSet<UnitController> _usedUnitsPool = new HashSet<UnitController>();

        private DateTime _startTime;
        #endregion

        #region PROPERTIES
        public int NetworkInputCount { get => _networkInputCount; }

        public int NetworkOutputCount { get => _networkOutputCount; }

        public uint CurrentGeneration { get; private set; }

        public double CurrentBestFitness { get; private set; }

        public NeatEvolutionAlgorithm<NeatGenome> EvolutionAlgorithm { get; private set; }

        public Experiment Experiment { get; private set; }
        #endregion

        #region UNTIY FUNCTIONS
        private void Start()
        {
            Utility.DebugLog = _enableDebugLogging;

            // load experiment config file and use it to create an Experiment
            XmlDocument xmlConfig = new XmlDocument();
            TextAsset textAsset = (TextAsset)Resources.Load(_experimentConfigFileName);

            if (textAsset == null)
            {
                Debug.LogError("The experiment config file named '" + _experimentConfigFileName + ".xml' could not be found in any Resources folder!");
                return;
            }

            xmlConfig.LoadXml(textAsset.text);

            Experiment = new Experiment();
            Experiment.Initialize(xmlConfig.DocumentElement, this, _networkInputCount, _networkOutputCount);

            ExperimentIO.DebugPrintSavePaths(Experiment);
        }
        #endregion

        #region NEAT LIFECYCLE
        /// <summary>
        /// Starts the NEAT algorithm.
        /// </summary>
        public void StartEvolution()
        {
            if (EvolutionAlgorithm != null && EvolutionAlgorithm.RunState == SharpNeat.Core.RunState.Running)
                return;

            DeactivateAllUnits();

            Utility.Log("Starting Experiment.");
            _startTime = DateTime.Now;

            EvolutionAlgorithm = Experiment.CreateEvolutionAlgorithm(ExperimentIO.GetSaveFilePath(Experiment.Name, ExperimentFileType.Population));
            EvolutionAlgorithm.UpdateEvent += new EventHandler(HandleUpdateEvent);
            EvolutionAlgorithm.PausedEvent += new EventHandler(HandlePauseEvent);
            EvolutionAlgorithm.StartContinue();
        }

        /// <summary>
        /// Stops the evaluation, resets all units and saves the current generation info to a file. When StartEA() is called again, that saved generation is loaded.
        /// </summary>
        public void StopEvolution()
        {
            DeactivateAllUnits();

            if (EvolutionAlgorithm != null && EvolutionAlgorithm.RunState == SharpNeat.Core.RunState.Running)
            {
                EvolutionAlgorithm.Stop();
            }
        }

        /// <summary>
        /// Stops the evolution in case its running and loads the current best Unit.
        /// </summary>
        public void RunBest()
        {
            StopEvolution();

            NeatGenome genome = Experiment.LoadChampion();
            if (genome == null)
                return;

            // Get a genome decoder that can convert genomes to phenomes.
            IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = Experiment.CreateGenomeDecoder();
            // Decode the genome into a phenome (neural network, i.e. IBlackBox).
            IBlackBox phenome = genomeDecoder.Decode(genome);

            ActivateUnit(phenome);
        }
        #endregion

        #region UNIT MANAGEMENT
        /// <summary>
        /// Get the Fitness of a Unit equipped with a IBlackBox (Neural Net).
        /// Called after a generation has performed, to evaluate the performance of a generation and to select the best of that generation for mating/mutation.
        /// </summary>
        public float GetFitness(IBlackBox box)
        {
            if (_blackBoxMap.ContainsKey(box))
            {
                return _blackBoxMap[box].GetFitness();
            }
            return 0;
        }

        /// <summary>
        /// Creates (or re-uses) a UnitController instance and assigns the Neural Net (IBlackBox) to it and activates it, so that it starts executing the Net.
        /// </summary>
        public void ActivateUnit(IBlackBox box)
        {
            UnitController controller = GetUnusedUnit(box);
            controller.ActivateUnit(box);
        }

        /// <summary>
        /// Deactivates and resets a Unit. Called after a generation has performed. 
        /// Units don't get Destroyed, instead they are just reset and re-used to avoid unneccessary instantiation calls. This process is called object pooling.
        /// </summary>
        public void DeactivateUnit(IBlackBox box)
        {
            if (_blackBoxMap.ContainsKey(box))
            {
                UnitController controller = _blackBoxMap[box];
                controller.DeactivateUnit();

                _blackBoxMap.Remove(box);
                PoolUnit(controller, false);
            }
        }

        /// <summary>
        /// Spawns a Unit. This means either reusing a deactivated unit from the pool or to instantiate a Unit into the pool, in case the pool is empty.
        /// Units don't get Destroyed, instead they are just reset to avoid unneccessary instantiation calls.
        /// </summary>
        private UnitController GetUnusedUnit(IBlackBox box)
        {
            UnitController controller;

            if (_unusedUnitsPool.Any())
            {
                controller = _unusedUnitsPool.First();
                _blackBoxMap.Add(box, controller);
            }
            else
            {
                controller = InstantiateUnit(box);
            }

            PoolUnit(controller, true);
            return controller;
        }

        /// <summary>
        /// Instantiates a Unit in case no Unit can be drawn from the _unusedUnitPool.
        /// </summary>
        private UnitController InstantiateUnit(IBlackBox box)
        {
            UnitController controller = Instantiate(_unitControllerPrefab, _unitControllerPrefab.transform.position, _unitControllerPrefab.transform.rotation);

            if (_spawnParent != null)
                controller.transform.parent = _spawnParent;
            else
                controller.transform.parent = this.transform;

            _blackBoxMap.Add(box, controller);
            return controller;
        }

        /// <summary>
        /// Puts Units into either the Unused or the Used object pool.
        /// </summary>
        private void PoolUnit(UnitController controller, bool markUsed)
        {
            if (markUsed)
            {
                _unusedUnitsPool.Remove(controller);
                _usedUnitsPool.Add(controller);
            }
            else
            {
                _unusedUnitsPool.Add(controller);
                _usedUnitsPool.Remove(controller);
            }
        }

        /// <summary>
        /// Destroys all UnitControllers and cleans the Object Pool.
        /// </summary>
        private void DeactivateAllUnits()
        {
            Dictionary<IBlackBox, UnitController> _blackBoxMapCopy = new Dictionary<IBlackBox, UnitController>(_blackBoxMap);

            foreach (KeyValuePair<IBlackBox, UnitController> boxUnitPair in _blackBoxMapCopy)
            {
                DeactivateUnit(boxUnitPair.Key);
            }
        }
        #endregion

        #region EVENT HANDLER
        /// <summary>
        /// Event callback which gets called at the end of each generation.
        /// </summary>
        void HandleUpdateEvent(object sender, EventArgs e)
        {
            Utility.Log(string.Format("Generation={0:N0} BestFitness={1:N6}", EvolutionAlgorithm.CurrentGeneration, EvolutionAlgorithm.Statistics._maxFitness));

            CurrentBestFitness = EvolutionAlgorithm.Statistics._maxFitness;
            CurrentGeneration = EvolutionAlgorithm.CurrentGeneration;
        }

        /// <summary>
        /// Event callback which gets called after the evolution got paused.
        /// </summary>
        void HandlePauseEvent(object sender, EventArgs e)
        {
            Utility.Log("STOP - Save the Population and the current Champion");

            // Save genomes to xml file.    
            Experiment.SavePopulation(EvolutionAlgorithm.GenomeList);
            Experiment.SaveChampion(EvolutionAlgorithm.CurrentChampGenome);

            DateTime endTime = DateTime.Now;
            Utility.Log("Total time elapsed: " + (endTime - _startTime));
        }
        #endregion
    }
}