using SmallMind.Core;
using System;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Extension methods for ModelMetadata to support pre-trained model information.
    /// </summary>
    public static class PretrainedModelMetadata
    {
        private const string TaskTypeKey = "TaskType";
        private const string DomainTypeKey = "DomainType";
        private const string ModelNameKey = "ModelName";
        private const string ModelDescriptionKey = "ModelDescription";
        private const string ModelVersionKey = "ModelVersion";
        private const string LabelsKey = "ClassificationLabels";

        /// <summary>
        /// Set the task type for a pre-trained model.
        /// </summary>
        public static void SetTaskType(this ModelMetadata metadata, TaskType taskType)
        {
            metadata.Extra[TaskTypeKey] = taskType.ToString();
        }

        /// <summary>
        /// Get the task type for a pre-trained model.
        /// </summary>
        public static TaskType GetTaskType(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(TaskTypeKey, out var value) && value is string str)
            {
                if (Enum.TryParse<TaskType>(str, out var taskType))
                {
                    return taskType;
                }
            }
            return TaskType.TextGeneration; // Default
        }

        /// <summary>
        /// Set the domain type for a pre-trained model.
        /// </summary>
        public static void SetDomainType(this ModelMetadata metadata, DomainType domainType)
        {
            metadata.Extra[DomainTypeKey] = domainType.ToString();
        }

        /// <summary>
        /// Get the domain type for a pre-trained model.
        /// </summary>
        public static DomainType GetDomainType(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(DomainTypeKey, out var value) && value is string str)
            {
                if (Enum.TryParse<DomainType>(str, out var domainType))
                {
                    return domainType;
                }
            }
            return DomainType.General; // Default
        }

        /// <summary>
        /// Set the model name.
        /// </summary>
        public static void SetModelName(this ModelMetadata metadata, string name)
        {
            metadata.Extra[ModelNameKey] = name;
        }

        /// <summary>
        /// Get the model name.
        /// </summary>
        public static string GetModelName(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(ModelNameKey, out var value) && value is string str)
            {
                return str;
            }
            return "Unnamed Model";
        }

        /// <summary>
        /// Set the model description.
        /// </summary>
        public static void SetModelDescription(this ModelMetadata metadata, string description)
        {
            metadata.Extra[ModelDescriptionKey] = description;
        }

        /// <summary>
        /// Get the model description.
        /// </summary>
        public static string GetModelDescription(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(ModelDescriptionKey, out var value) && value is string str)
            {
                return str;
            }
            return string.Empty;
        }

        /// <summary>
        /// Set the model version.
        /// </summary>
        public static void SetModelVersion(this ModelMetadata metadata, string version)
        {
            metadata.Extra[ModelVersionKey] = version;
        }

        /// <summary>
        /// Get the model version.
        /// </summary>
        public static string GetModelVersion(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(ModelVersionKey, out var value) && value is string str)
            {
                return str;
            }
            return "1.0.0";
        }

        /// <summary>
        /// Set classification labels for text classification models.
        /// </summary>
        public static void SetClassificationLabels(this ModelMetadata metadata, string[] labels)
        {
            metadata.Extra[LabelsKey] = string.Join(",", labels);
        }

        /// <summary>
        /// Get classification labels for text classification models.
        /// </summary>
        public static string[] GetClassificationLabels(this ModelMetadata metadata)
        {
            if (metadata.Extra.TryGetValue(LabelsKey, out var value) && value is string str)
            {
                return str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            return Array.Empty<string>();
        }
    }
}
