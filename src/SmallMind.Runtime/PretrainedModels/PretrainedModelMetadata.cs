using SmallMind.Core;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Extension methods for ModelMetadata to support pre-trained model information.
    /// </summary>
    internal static class PretrainedModelMetadata
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
            if (metadata.Extra.TryGetValue(TaskTypeKey, out var value))
            {
                // Handle both string and JsonElement cases (after deserialization)
                string? strValue = value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
                    _ => value?.ToString()
                };

                if (!string.IsNullOrEmpty(strValue) && Enum.TryParse<TaskType>(strValue, out var taskType))
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
            if (metadata.Extra.TryGetValue(DomainTypeKey, out var value))
            {
                // Handle both string and JsonElement cases (after deserialization)
                string? strValue = value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
                    _ => value?.ToString()
                };

                if (!string.IsNullOrEmpty(strValue) && Enum.TryParse<DomainType>(strValue, out var domainType))
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
            if (metadata.Extra.TryGetValue(ModelNameKey, out var value))
            {
                // Handle both string and JsonElement cases
                return value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? "Unnamed Model",
                    _ => value?.ToString() ?? "Unnamed Model"
                };
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
            if (metadata.Extra.TryGetValue(ModelDescriptionKey, out var value))
            {
                // Handle both string and JsonElement cases
                return value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? string.Empty,
                    _ => value?.ToString() ?? string.Empty
                };
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
            if (metadata.Extra.TryGetValue(ModelVersionKey, out var value))
            {
                // Handle both string and JsonElement cases
                return value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? "1.0.0",
                    _ => value?.ToString() ?? "1.0.0"
                };
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
            if (metadata.Extra.TryGetValue(LabelsKey, out var value))
            {
                // Handle both string and JsonElement cases
                string? strValue = value switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
                    _ => value?.ToString()
                };

                if (!string.IsNullOrEmpty(strValue))
                {
                    return strValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }
            return Array.Empty<string>();
        }
    }
}
