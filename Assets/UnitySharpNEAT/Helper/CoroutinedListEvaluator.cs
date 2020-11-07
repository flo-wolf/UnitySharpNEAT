/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeat.Core;
using System.Collections;
using UnityEngine;
using SharpNeat.Phenomes;

namespace UnitySharpNEAT
{
    /// <summary>
    /// Evaluates the fitness of a List of genomes through the use of Coroutines. 
    /// The evaluator first encodes the genomes into phenomes (IBlackBox) and assigns them to the Units manged by the NeatSupervisor.
    /// After each TrailDuration for the Amount of Trails specified in the NeatSupervisor the population of phenomes is evaluated.
    /// After all trials have been completed, the average fitness for each phenome is computed and the Unit gets Deactivated again.
    /// That fitness is then assigned to the associated genome, which will be used in the next generation by the genetic algorithm for selection.
    /// </summary>
    [Serializable]
    class CoroutinedListEvaluator<TGenome, TPhenome> : IGenomeListEvaluator<TGenome>
        where TGenome : class, IGenome<TGenome>
        where TPhenome : class
    {
        [SerializeField] 
        private readonly IGenomeDecoder<TGenome, TPhenome> _genomeDecoder;

        [SerializeField] 
        private IPhenomeEvaluator<TPhenome> _phenomeEvaluator;

        [SerializeField] 
        private NeatSupervisor _neatSupervisor;

        #region Constructor
        /// <summary>
        /// Construct with the provided IGenomeDecoder and IPhenomeEvaluator.
        /// </summary>
        public CoroutinedListEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
                                         IPhenomeEvaluator<TPhenome> phenomeEvaluator,
                                          NeatSupervisor neatSupervisor)
        {
            _genomeDecoder = genomeDecoder;
            _phenomeEvaluator = phenomeEvaluator;
            _neatSupervisor = neatSupervisor;
        }

        #endregion

        public ulong EvaluationCount
        {
            get { return _phenomeEvaluator.EvaluationCount; }
        }

        public bool StopConditionSatisfied
        {
            get { return _phenomeEvaluator.StopConditionSatisfied; }
        }

        public IEnumerator Evaluate(IList<TGenome> genomeList)
        {
            yield return EvaluateList(genomeList);
        }

        // called by NeatEvolutionAlgorithm at the beginning of a generation
        private IEnumerator EvaluateList(IList<TGenome> genomeList)
        {
            Reset();

            Dictionary<TGenome, TPhenome> dict = new Dictionary<TGenome, TPhenome>();
            Dictionary<TGenome, FitnessInfo[]> fitnessDict = new Dictionary<TGenome, FitnessInfo[]>();
            for (int i = 0; i < _neatSupervisor.Trials; i++)
            {
                Utility.Log("UnityParallelListEvaluator.EvaluateList -- Begin Trial " + (i + 1));
                //_phenomeEvaluator.Reset();  // _phenomeEvaluator = SimpleEvalutator instance, created in Experiment.cs
                if (i == 0)
                {
                    foreach (TGenome genome in genomeList)
                    {
                        // TPhenome = IBlackbox. Basically we create a blackBox from the genome.
                        TPhenome phenome = _genomeDecoder.Decode(genome);   // _genomeDecoder = NeatGenomeDecoder instance, created during the creation of the EvolutionAlgorithm in in Experiment.cs

                        if (phenome == null)
                        {
                            // Non-viable genome.
                            genome.EvaluationInfo.SetFitness(0.0);
                            genome.EvaluationInfo.AuxFitnessArr = null;
                        }
                        else
                        {
                            // Assign the IBlackBox to a unit and let the Unit perform
                            _neatSupervisor.ActivateUnit((IBlackBox)phenome);

                            fitnessDict.Add(genome, new FitnessInfo[_neatSupervisor.Trials]);
                            dict.Add(genome, phenome);
                        }
                    }
                } 
           
                // wait until the next trail, i.e. when the next evaluation should happen
                yield return new WaitForSeconds(_neatSupervisor.TrialDuration);

                // evaluate the fitness of all phenomes (IBlackBox) during this trial duration.
                foreach (TGenome genome in dict.Keys)
                {
                    TPhenome phenome = dict[genome];

                    if (phenome != null)
                    {
                        _phenomeEvaluator.Evaluate(phenome);
                        FitnessInfo fitnessInfo = _phenomeEvaluator.GetLastFitness(phenome);
                        
                        fitnessDict[genome][i] = fitnessInfo;
                    }
                }
            }

            // Get the combined fitness for all trials of each genome and save that Fitnessinfo for each genome. 
            // Then deactivate the Unit to finish this generation.
            foreach (TGenome genome in dict.Keys)
            {
                TPhenome phenome = dict[genome];
                if (phenome != null)
                {
                    double fitness = 0;

                    for (int i = 0; i < _neatSupervisor.Trials; i++)
                    {
                        fitness += fitnessDict[genome][i]._fitness;
                    }
                    var fit = fitness;
                    fitness /= _neatSupervisor.Trials; // Averaged fitness
                    
                    if (fitness > _neatSupervisor.StoppingFitness)
                    {
                      Utility.Log("Fitness is " + fit + ", stopping now because stopping fitness is " + _neatSupervisor.StoppingFitness);
                      //  _phenomeEvaluator.StopConditionSatisfied = true;
                    }
                    genome.EvaluationInfo.SetFitness(fitness);
                    genome.EvaluationInfo.AuxFitnessArr = fitnessDict[genome][0]._auxFitnessArr;

                    // The phenome has performed, deactivate the Unit the phenome was assigned to.
                    _neatSupervisor.DeactivateUnit((IBlackBox)phenome);
                }
            }
            yield return 0;
        }

        public void Reset()
        {
            _phenomeEvaluator.Reset();
        }
    }
}
