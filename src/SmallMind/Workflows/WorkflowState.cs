using System;
using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Stateful container for workflow execution context.
    /// Stores key-value pairs and reserved metadata.
    /// </summary>
    public class WorkflowState
    {
        private readonly Dictionary<string, object> _state = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _stepOutputs = new Dictionary<string, string>();

        /// <summary>
        /// Workflow metadata (run id, timestamps, etc.).
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Get a value from state as a string.
        /// </summary>
        public string? GetString(string key)
        {
            if (_state.TryGetValue(key, out var value))
            {
                return value?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Get a value from state as an integer.
        /// </summary>
        public int? GetInt(string key)
        {
            if (_state.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (int.TryParse(value?.ToString(), out var parsed))
                    return parsed;
            }
            return null;
        }

        /// <summary>
        /// Try to get a typed value from state.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            if (_state.TryGetValue(key, out var obj))
            {
                if (obj is T typed)
                {
                    value = typed;
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Set a value in state.
        /// </summary>
        public void Set(string key, object value)
        {
            _state[key] = value;
        }

        /// <summary>
        /// Check if a key exists in state.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return _state.ContainsKey(key);
        }

        /// <summary>
        /// Get all state keys.
        /// </summary>
        public IEnumerable<string> GetKeys()
        {
            return _state.Keys;
        }

        /// <summary>
        /// Get raw state dictionary (for context building).
        /// </summary>
        public IReadOnlyDictionary<string, object> GetState()
        {
            return _state;
        }

        /// <summary>
        /// Record output for a specific step.
        /// </summary>
        public void SetStepOutput(string stepId, string output)
        {
            _stepOutputs[stepId] = output;
        }

        /// <summary>
        /// Get output for a specific step.
        /// </summary>
        public string? GetStepOutput(string stepId)
        {
            return _stepOutputs.TryGetValue(stepId, out var output) ? output : null;
        }

        /// <summary>
        /// Get all step outputs.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllStepOutputs()
        {
            return _stepOutputs;
        }
    }
}
