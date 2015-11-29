// <copyright file="Scripts.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.SqlServer.Database
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal class Scripts
    {
        private static readonly string RootNamespace = typeof(Scripts).FullName;
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

        private readonly Dictionary<int, string[]> databaseScripts = new Dictionary<int, string[]>();

        public Scripts(string schema)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.GetDatabaseVersion = GetScript("GetDatabaseVersion").Replace("dbo", schema);
            this.CreateSchema = GetScript("CreateSchema").Replace("dbo", schema);

            var resourceNames = ExecutingAssembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(string.Concat(RootNamespace, ".Version")));

            foreach (var resourceName in resourceNames)
            {
                var expectedVersion = this.databaseScripts.Count + 1;
                var actualVersion = int.Parse(
                    resourceName
                        .Replace(string.Concat(RootNamespace, ".Version"), string.Empty)
                        .Replace(".sql", string.Empty));

                if (expectedVersion != actualVersion)
                {
                    // TODO (Cameron): Fix message.
                    throw new Exception("mess");
                }

                var versionScripts = new List<string>();

                using (var stream = ExecutingAssembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    do
                    {
                        var line = reader.ReadLine();

                        if (line.TrimStart().StartsWith("-- SQL:", StringComparison.OrdinalIgnoreCase))
                        {
                            var sb = new StringBuilder();

                            do
                            {
                                sb.AppendLine(line);
                                line = reader.ReadLine();
                            }
                            while (!line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase));

                            versionScripts.Add(sb.ToString().Replace("dbo", schema));
                        }
                    }
                    while (!reader.EndOfStream);
                }

                this.databaseScripts.Add(actualVersion, versionScripts.ToArray());
            }
        }

        public string GetDatabaseVersion { get; private set; }

        public string CreateSchema { get; private set; }

        public IReadOnlyDictionary<int, string[]> Database
        {
            get { return this.databaseScripts; }
        }

        public int SupportedVersion
        {
            get { return this.databaseScripts.Count; }
        }

        private static string GetScript(string name)
        {
            using (var stream = ExecutingAssembly.GetManifestResourceStream(string.Concat(RootNamespace, ".", name, ".sql")))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
