/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using SharpNeat.Core;
using SharpNeat.Domains;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace UnitySharpNEAT
{
    public enum ExperimentFileType { Champion, Population }

    /// <summary>
    /// This class handles saving and loading the population of a INeatExperiment.
    /// When saved, the whole population of genomes and/or the current best genome is saved to an XML file, where it can be loaded from again.
    /// The Experiment's 'Name' variable will be part of the savefile name and as such used as an identifier. 
    /// Thus the experimetn name should only be changed with caution, since savefiles already associated with it would need to be manually renamed to remain associated with the experiment.
    /// </summary>
    public static class ExperimentIO
    {
        public const string FILE_EXTENTION_POPULATION = ".pop.xml";
        public const string FILE_EXTENTION_CHAMPION = ".champ.xml";

        /// <summary>
        /// Get the absolute path to the requested filetype (population/champion savefile).
        /// </summary>
        public static string GetSaveFilePath(string experimentName, ExperimentFileType fileType)
        {
            string extention;
            switch (fileType)
            {
                case ExperimentFileType.Champion:
                    extention = FILE_EXTENTION_CHAMPION;
                    break;
                case ExperimentFileType.Population:
                    extention = FILE_EXTENTION_POPULATION;
                    break;
                default:
                    extention = ".UnknownNeatFileType";
                    break;
            }
            return Application.persistentDataPath + "/" + experimentName + extention;
        }

        /// <summary>
        /// Writes the specified genomes to the population safe file of the specified experiment (by default: myexperimentname.pop.xml)
        /// </summary>
        public static bool WritePopulation(INeatExperiment experiment, IList<NeatGenome> genomeList)
        {
            return WriteGenomes(experiment, genomeList, ExperimentFileType.Population);
        }

        /// <summary>
        /// Writes the specified genome to the champion safe file of the specified experiment (by default: myexperimentname.champ.xml)
        /// </summary>
        public static bool WriteChampion(INeatExperiment experiment, NeatGenome bestGenome)
        {
            return WriteGenomes(experiment, new NeatGenome[] { bestGenome }, ExperimentFileType.Champion);
        }

        /// <summary>
        /// Writes a list of genomes to the save file fitting the experiment name and the ExperimentFileType.
        /// </summary>
        private static bool WriteGenomes(INeatExperiment experiment, IList<NeatGenome> genomeList, ExperimentFileType fileType)
        {
            XmlWriterSettings _xwSettings = new XmlWriterSettings();
            _xwSettings.Indent = true;

            string filePath = GetSaveFilePath(experiment.Name, fileType);
            
            DirectoryInfo dirInf = new DirectoryInfo(Application.persistentDataPath);
            if (!dirInf.Exists)
            {
                Debug.Log("ExperimentIO - Creating subdirectory");
                dirInf.Create();
            }
            try
            {
                using (XmlWriter xw = XmlWriter.Create(filePath, _xwSettings))
                {
                    NeatGenomeXmlIO.WriteComplete(xw, genomeList, false);
                    Debug.Log("Successfully saved the genomes of the '" + fileType.ToString() + "' for the experiment '" + experiment.Name + "' to the location:\n" + filePath);
                }
            }
            catch (Exception e1)
            {
                Debug.Log("Error saving the genomes of the '" + fileType.ToString() + "' for the experiment '" + experiment.Name + "' to the location:\n" + filePath);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Loads the saved population from the population safe file of the specified experiment (by default: myexperimentname.pop.xml).
        /// If the file does not exist, then a new population is created and returned.
        /// </summary>
        public static List<NeatGenome> ReadPopulation(INeatExperiment experiment)
        {
            return ReadGenomes(experiment, ExperimentFileType.Population);
        }

        /// <summary>
        /// Loads the saved champion genome from the champion safe file of the specified experiment (by default: myexperimentname.champ.xml).
        /// If the file does not exist, then null is returned.
        /// </summary>
        public static NeatGenome ReadChampion(INeatExperiment experiment)
        {
            List<NeatGenome> championPop = ReadGenomes(experiment, ExperimentFileType.Champion, false);
            if (championPop == null || championPop.Count == 0)
                return null;

            return championPop[0];
        }

        /// <summary>
        /// Loads a list of genomes from the save file fitting the experiment name and the ExperimentFileType.
        /// </summary>
        private static List<NeatGenome> ReadGenomes(INeatExperiment experiment, ExperimentFileType fileType, bool createNewGenesIfNotLoadable = true)
        {
            List<NeatGenome> genomeList = null;
            NeatGenomeFactory genomeFactory = (NeatGenomeFactory)experiment.CreateGenomeFactory();

            string filePath = GetSaveFilePath(experiment.Name, fileType);

            try
            {
                using (XmlReader xr = XmlReader.Create(filePath))
                {
                    genomeList = NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, genomeFactory);

                    if(genomeList != null && genomeList.Count > 0)
                        Utility.Log("Successfully loaded the genomes of the '" + fileType.ToString() + "' for the experiment '" + experiment.Name + "' from the location:\n" + filePath);
                }
            }
            catch (Exception e1)
            {
                Utility.Log("Error loading genome from file, could not find the file at: " + filePath + "\n" + e1.Message);

                if(createNewGenesIfNotLoadable)
                    genomeList = genomeFactory.CreateGenomeList(experiment.DefaultPopulationSize, 0);
            }
            return genomeList;
        }

        /// <summary>
        /// Deletes a certain savefile which fits the ExperimentFileType of the specified experiment
        /// </summary>
        public static void DeleteSaveFile(INeatExperiment experiment, ExperimentFileType fileType)
        {
            File.Delete(GetSaveFilePath(experiment.Name, fileType));
            Debug.Log("ExperimentIO - Deleted savefile of type: " + fileType);
        }

        /// <summary>
        /// Deletes all savefiles of the specified experiment, which is the population file and the champion file (best genome).
        /// </summary>
        public static void DeleteAllSaveFiles(INeatExperiment experiment)
        {
            File.Delete(GetSaveFilePath(experiment.Name, ExperimentFileType.Champion));
            File.Delete(GetSaveFilePath(experiment.Name, ExperimentFileType.Population));

            Debug.Log("ExperimentIO - Deleted all savefiles.");
        }

        /// <summary>
        /// Display all save paths for debugging purposes. By default they are printed at application start, once.
        /// </summary>
        public static void DebugPrintSavePaths(INeatExperiment experiment)
        {
            Debug.Log("ExperimentIO - Population save file path: " + GetSaveFilePath(experiment.Name, ExperimentFileType.Population));
            Debug.Log("ExperimentIO - Champion save file path: " + GetSaveFilePath(experiment.Name, ExperimentFileType.Champion));
        }
    }
}