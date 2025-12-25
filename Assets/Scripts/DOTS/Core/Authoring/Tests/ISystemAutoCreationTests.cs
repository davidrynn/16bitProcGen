using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;

namespace DOTS.Core.Authoring.Tests
{
    public class ISystemAutoCreationTests
    {
        [Test]
        public void AllProjectISystemsHaveDisableAutoCreation()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => ProjectAssemblyNames.Contains(assembly.GetName().Name))
                .ToArray();

            var missingAttributes = new List<Type>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(ISystem).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                    {
                        continue;
                    }

                    if (!Attribute.IsDefined(type, typeof(DisableAutoCreationAttribute)))
                    {
                        missingAttributes.Add(type);
                    }
                }
            }

            if (missingAttributes.Count > 0)
            {
                var message = "ISystem types missing [DisableAutoCreation]:\n" +
                              string.Join("\n", missingAttributes.Select(type => type.FullName));
                Assert.Fail(message);
            }
        }

        private static readonly HashSet<string> ProjectAssemblyNames = new HashSet<string>
        {
            "Core",
            "Player",
            "DOTS.Player.Bootstrap",
            "DOTS.Terrain",
            "DOTS.Core.Authoring"
        };
    }
}
