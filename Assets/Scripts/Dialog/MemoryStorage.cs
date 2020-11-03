using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace Dialog
{
    /// <summary>
    /// Modified version of InMemoryVariableStorage
    /// </summary>
    /// todo: speed up by using dirty flags
    public class MemoryStorage : InMemoryVariableStorage
    {
        public override void SetValue(string variableName, float floatValue)
        {
            Debug.Log($"{variableName} = {floatValue}");
            base.SetValue(variableName, floatValue);
        }

        public void Write(SaveData saveData)
        {
            saveData.savedVariables.Clear();
            foreach (var pair in this.AsEnumerable())
            {
                saveData.savedVariables.Add(
                    new DefaultVariable
                    {
                        name = pair.Key.Remove(0, 1),
                        value = pair.Value.AsString,
                        type = pair.Value.type
                    }
                );
            }
        }
    }
}