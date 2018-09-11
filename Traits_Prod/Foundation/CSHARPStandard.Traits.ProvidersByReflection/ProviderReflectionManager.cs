﻿/********************************************************************************
 * CSHARP Traits Data Object Library 
 * 
 * LICENSE: Free to use provided details on fixes and/or extensions emailed to 
 *          chris.williams@readwatchcreate.com
 ********************************************************************************/

namespace CSHARPStandard.Traits.ProvidersByReflection
{
    using CSHARPStandard.IO;
    using CSHARPStandard.Reflection;
    using CSHARPStandard.Traits.Business;
    using CSHARPStandard.Traits.Data;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Manages loading providers via reflection based on Traits
    /// </summary>
    public class ProviderReflectionManager
    {
        /// <summary>
        /// Traits used to look up provider reflection settings.
        /// </summary>
        /// <remarks>ASSUMES all trait variables already resolved</remarks>
        public ITraits Traits { get; set; }

        /// <summary>
        /// Root path to find Providers
        /// </summary>
        public string ProviderRootPath { get; set; }

        #region Provider Related

        /// <summary>
        /// Gets a list of the names of all providers of a given type
        /// </summary>
        /// <param name="objectType">Object Type eg. ACTIVITY</param>
        /// <returns></returns>
        /// <remarks>ASSUMES all trait variables already resolved</remarks>
        public List<string> GetAllProviderNamesOfObjectType(string objectType)
        {
            if (Traits == null || Traits.TraitPairs == null) throw new ArgumentNullException("ERROR: Traits property must be assigned");
            if (string.IsNullOrEmpty(objectType)) throw new ArgumentNullException("ERROR: objectType is required");

            var providerNames = new List<string>();

            foreach (var trait in Traits.TraitPairs.Where(t => t.FieldName.StartsWith("PROVIDER_" + objectType.ToUpperInvariant() + "_")).ToList())
            {
                providerNames.Add(trait.FieldName.Replace("PROVIDER_" + objectType.ToUpperInvariant() + "_", string.Empty));
            }

            return providerNames;
        }

        /// <summary>
        /// Gets the data source for the provider
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="objectType"></param>
        /// <remarks>ASSUMES all trait variables already resolved</remarks>
        public string GetProviderDataSource(string providerName, string objectType)
        {
            if (Traits == null || Traits.TraitPairs == null) throw new ArgumentNullException("ERROR: Traits property must be assigned");
            if (string.IsNullOrEmpty(objectType)) throw new ArgumentNullException("ERROR: objectType is required");
            if (string.IsNullOrEmpty(providerName)) throw new ArgumentNullException("ERROR: providerName is required");

            // Get the raw data source
            var providerEntry = Traits.TraitPairs.FirstOrDefault(t => t.FieldName == "PROVIDER_" + objectType.ToUpperInvariant() + "_" + providerName.ToUpperInvariant());
            if (providerEntry == null) throw new Exception("ERROR Provider not found in Traits (" + objectType.ToUpperInvariant() + "_" + providerName.ToUpperInvariant() + ")");

            // Apply the trait before returning
            var traitHelper = new TraitHelper();
            var appliedProviderEntry = traitHelper.ApplyTrait(Traits, providerEntry);

            // Return the data source
            var providerEntryParts = appliedProviderEntry.FieldValue.Split(',');
            return providerEntryParts[2];
        }

        /// <summary>
        /// Loads the provider object based on entries in traits (Can be overridden for dervived Dain)
        /// </summary>
        /// <param name="providerName">Name of provider</param>
        /// <param name="objectType">type of object we are loading</param>
        /// <returns></returns>
        /// <remarks>ASSUMES all trait variables already resolved. Creates instance from Assembly, if not overriden in trait for provider
        /// v0.0.1.1 - Wrong brackets strip off ddl name
        /// </remarks>
        public virtual object LoadProvider(string providerName, string objectType)
        {
            if (string.IsNullOrEmpty(ProviderRootPath)) throw new ArgumentNullException("ERROR ProviderRootPath is required to load providers");

            if (Traits == null) throw new ArgumentNullException("ERROR Traits are required to load provider");
            if (string.IsNullOrEmpty(providerName)) throw new ArgumentException("providerName is required to load provider");
            if (string.IsNullOrEmpty(objectType)) throw new ArgumentException("objectType is required to load provider");

            var reflectionManager = new ReflectionManager();
            var fileHelper = new FileHelper();

            // Look up the provider info 
            var providerEntry = Traits.TraitPairs.FirstOrDefault(t => t.FieldName == "PROVIDER_" + objectType.ToUpperInvariant() + "_" + providerName.ToUpperInvariant());
            if (providerEntry == null) throw new Exception("ERROR Provider not found in Traits (PROVIDER_" + objectType.ToUpperInvariant() + "_" + providerName.ToUpperInvariant() + ")");

            // Apply the trait before returning
            var traitHelper = new TraitHelper();
            var appliedProviderEntry = traitHelper.ApplyTrait(Traits, providerEntry);

            var providerEntryParts = appliedProviderEntry.FieldValue.Split(',');
            var providerAssemblyName = providerEntryParts[0];
            var providerTypeName = providerEntryParts[1];
            var providerDataSource = providerEntryParts[2];

            // Get provider assembly folder, if supplied
            var providerFolder = (providerEntryParts.Length > 3) ? providerEntryParts[3] : string.Empty;

            // Check if we should create instance using Accessor.
            bool useAccessor = (providerEntryParts.Length > 4) 
                ? string.IsNullOrEmpty(providerEntryParts[4]) == false 
                    ? Convert.ToBoolean(providerEntryParts[4]) 
                    : false
                :false;

            // if assembly not found report error 
            if (string.IsNullOrEmpty(providerAssemblyName)) throw new NullReferenceException("Assembly Name not found in config for: " + objectType.ToUpperInvariant() + " : " + providerName.ToUpperInvariant());

            // if type name not found report error
            if (string.IsNullOrEmpty(providerTypeName)) throw new NullReferenceException("Type Name not found in config for:" + objectType.ToUpperInvariant() + " : " + providerTypeName);

            // if provider folder contains relative path then we need to combine the path
            if(providerFolder.Contains("..\\")) providerFolder = Path.Combine(ProviderRootPath, providerFolder);

            // Load the provider object
            var loadedObject = (useAccessor == true
                                    ? reflectionManager.LoadObjectUsingActivator(fileHelper.EnsureTrailingDirectorySeparator(ProviderRootPath) + (string.IsNullOrEmpty(providerFolder) ? string.Empty : fileHelper.EnsureTrailingDirectorySeparator(providerFolder) + providerAssemblyName), providerTypeName)
                                    : reflectionManager.LoadObject(
                                        // if the provider folder contains a : then it is a full path so don't use ProviderRootPath
                                        (providerFolder.Contains(":") 
                                            ? fileHelper.EnsureTrailingDirectorySeparator(providerFolder) + providerAssemblyName
                                            : fileHelper.EnsureTrailingDirectorySeparator(ProviderRootPath) + (string.IsNullOrEmpty(providerFolder) ? string.Empty : fileHelper.EnsureTrailingDirectorySeparator(providerFolder)) + providerAssemblyName)
                                        , providerTypeName)
                                );

            return loadedObject;
        }

        #endregion
    }
}
