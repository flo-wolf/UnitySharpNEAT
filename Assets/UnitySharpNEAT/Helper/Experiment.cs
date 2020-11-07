/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using SharpNeat.Core;
using SharpNeat.Decoders;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.Domains;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;
using SharpNeat.SpeciationStrategies;
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;


namespace UnitySharpNEAT
{
    /// <summary>
    /// The Experiment is what creates the evolution algorithm, which will evolve the Units using NEAT.
    /// It is also responsible for loading/saving the current population under the Experiment's 'Name' identifier.
    /// </summary>
    [Serializable]
    public class Experiment : INeatExperiment
    {
        #region MEMBER VARIABLES
        [SerializeField]
        private NeatEvolutionAlgorithmParameters _eaParams;

        [SerializeField]
        private NeatGenomeParameters _neatGenomeParams;

        [SerializeField]
        private string _name;

        [SerializeField]
        private int _populationSize;

        [SerializeField]
        private int _specieCount;

        [SerializeField]
        private NetworkActivationScheme _activationScheme;

        [SerializeField]
        private string _complexityRegulationStr;

        [SerializeField]
        private int? _complexityThreshold;

        [SerializeField]
        private string _description;

        [SerializeField]
        private NeatSupervisor _neatSupervisor;

        [SerializeField]
        private int _inputCount;

        [SerializeField]
        private int _outputCount;
        #endregion

        #region PROPERTIES
        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _description; }
        }

        public int InputCount
        {
            get { return _inputCount; }
        }

        public int OutputCount
        {
            get { return _outputCount; }
        }

        public int DefaultPopulationSize
        {
            get { return _populationSize; }
        }

        public NeatEvolutionAlgorithmParameters NeatEvolutionAlgorithmParameters
        {
            get { return _eaParams; }
        }

        public NeatGenomeParameters NeatGenomeParameters
        {
            get { return _neatGenomeParams; }
        }
        #endregion

        #region FUNCTIONS
        public void Initialize(XmlElement xmlConfig, NeatSupervisor neatSupervisor, int inputCount, int outputCount)
        {
            _name = XmlUtils.TryGetValueAsString(xmlConfig, "ExperimentName");
            _populationSize = XmlUtils.GetValueAsInt(xmlConfig, "PopulationSize");
            _specieCount = XmlUtils.GetValueAsInt(xmlConfig, "SpecieCount");
            _activationScheme = ExperimentUtils.CreateActivationScheme(xmlConfig, "Activation");
            _complexityRegulationStr = XmlUtils.TryGetValueAsString(xmlConfig, "ComplexityRegulationStrategy");
            _complexityThreshold = XmlUtils.TryGetValueAsInt(xmlConfig, "ComplexityThreshold");
            _description = XmlUtils.TryGetValueAsString(xmlConfig, "Description");

            _eaParams = new NeatEvolutionAlgorithmParameters();
            _eaParams.SpecieCount = _specieCount;
            _neatGenomeParams = new NeatGenomeParameters();
            _neatGenomeParams.FeedforwardOnly = _activationScheme.AcyclicNetwork;

            _neatSupervisor = neatSupervisor;

            _inputCount = inputCount;
            _outputCount = outputCount;
        }

        public IGenomeDecoder<NeatGenome, IBlackBox> CreateGenomeDecoder()
        {
            return new NeatGenomeDecoder(_activationScheme);
        }

        public IGenomeFactory<NeatGenome> CreateGenomeFactory()
        {
            return new NeatGenomeFactory(InputCount, OutputCount, _neatGenomeParams);
        }

        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(string fileName)
        {
            List<NeatGenome> genomeList = LoadPopulation();
            IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();
            return CreateEvolutionAlgorithm(genomeFactory, genomeList);
        }

        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm()
        {
            return CreateEvolutionAlgorithm(_populationSize);
        }

        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(int populationSize)
        {
            IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();

            List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(populationSize, 0);

            return CreateEvolutionAlgorithm(genomeFactory, genomeList);
        }

        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(IGenomeFactory<NeatGenome> genomeFactory, List<NeatGenome> genomeList)
        {
            IDistanceMetric distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);
            ISpeciationStrategy<NeatGenome> speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(distanceMetric);

            IComplexityRegulationStrategy complexityRegulationStrategy = ExperimentUtils.CreateComplexityRegulationStrategy(_complexityRegulationStr, _complexityThreshold);

            NeatEvolutionAlgorithm<NeatGenome> ea = new NeatEvolutionAlgorithm<NeatGenome>(_eaParams, speciationStrategy, complexityRegulationStrategy);

            // Create black box evaluator       
            BlackBoxFitnessEvaluator evaluator = new BlackBoxFitnessEvaluator(_neatSupervisor);

            IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = CreateGenomeDecoder();

            IGenomeListEvaluator<NeatGenome> innerEvaluator = new CoroutinedListEvaluator<NeatGenome, IBlackBox>(genomeDecoder, evaluator, _neatSupervisor);

            IGenomeListEvaluator<NeatGenome> selectiveEvaluator = new SelectiveGenomeListEvaluator<NeatGenome>(innerEvaluator,
                SelectiveGenomeListEvaluator<NeatGenome>.CreatePredicate_OnceOnly());

            //ea.Initialize(selectiveEvaluator, genomeFactory, genomeList);
            ea.Initialize(innerEvaluator, genomeFactory, genomeList);

            return ea;
        }
        #endregion

        #region POPULATION SAVING/LOADING
        /// <summary>
        /// Saves the specified genomes to the population safe file of this experiment (by default: myexperimentname.pop.xml)
        /// </summary>
        public bool SavePopulation(IList<NeatGenome> genomeList)
        {
            return ExperimentIO.WritePopulation(this, genomeList);
        }

        /// <summary>
        /// Loads the saved population from the population safe file of this experiment (by default: myexperimentname.pop.xml).
        /// If the file does not exist, then a new population is created and returned.
        /// </summary>
        public List<NeatGenome> LoadPopulation()
        {
            return ExperimentIO.ReadPopulation(this);
        }

        /// <summary>
        /// Saves the specified genome to the champion safe file of this experiment (by default: myexperimentname.champ.xml)
        /// </summary>
        public bool SaveChampion(NeatGenome bestGenome)
        {
            return ExperimentIO.WriteChampion(this, bestGenome);
        }

        /// <summary>
        /// Loads the saved champion genome from the champion safe file of this experiment (by default: myexperimentname.champ.xml).
        /// If the file does not exist, then null is returned
        /// </summary>
        public NeatGenome LoadChampion()
        {
            return ExperimentIO.ReadChampion(this);
        }
        #endregion
    }
}