// <copyright file="SqlServerMementoPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using dddlib.Configuration;
    using dddlib.Persistence.SqlServer;
    using dddlib.Persistence.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;
    using Xunit;

    // As someone who uses dddlib
    // In order save state
    // I need to be able to persist an aggregate root
    public abstract class SqlServerMementoPersistence : SqlServerFeature
    {
        public SqlServerMementoPersistence(SqlServerFixture fixture)
            : base(fixture)
        {
        }

        /*
            AggregateRoot Persistence (Guid)
            --------------------------------
            with natural key selector (undefined) AND with uninitialized factory (undefined)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (undefined)
            with natural key selector (undefined) AND with uninitialized factory (defined in bootstrapper only)
            with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)

            AggregateRoot Persistence (special case: string)
            ------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key equality comparer (undefined)
            with natural key equality comparer (string only, defined in bootstrapper only)

            AggregateRoot Persistence (special case: composite key value object: strings)
            -----------------------------------------------------------------------------
            ALL FOLLOWING TEST: with natural key selector (defined - doesn't matter how) AND with uninitialized factory (defined in bootstrapper only)
            with natural key serializer (undefined)
            with natural key serializer (defined in bootstrapper)
            AND MORE?
        */

        public class DefaultSqlServerPersistence : SqlServerMementoPersistence
        {
            public DefaultSqlServerPersistence(SqlServerFixture fixture)
                : base(fixture)
            {
            }

            [Scenario]
            public void Scenario(IRepository<Subject> repository, Subject instance, Subject otherInstance, string naturalKey)
            {
                "Given a SQL database"
                    .f(() => this.ExecuteSql(@"CREATE TABLE [dbo].[Subjects]
(
    [Id] [uniqueidentifier] NOT NULL,
    [NaturalKey] [varchar](MAX) NOT NULL,
    [State] [varchar](20) NOT NULL,
    CONSTRAINT [PK_Id] PRIMARY KEY CLUSTERED ([Id])
);"));

                "And a repository"
                    .f(() => repository = new SubjectRepository(this.ConnectionString));

                "And a natural key value"
                    .f(() => naturalKey = "key");

                "And an instance of an entity with that natural key"
                    .f(() => instance = new Subject(naturalKey));

                "When that instance is saved to the repository"
                    .f(() => repository.Save(instance));

                "And an other instance is loaded from the repository"
                    .f(() => otherInstance = repository.Load(instance.NaturalKey));

                "Then that instance should be the other instance"
                    .f(() => instance.Should().Be(otherInstance));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string naturalKey)
                {
                    this.Apply(new NewSubject { NaturalKey = naturalKey });
                }

                internal Subject()
                {
                }

                public string NaturalKey { get; private set; }

                protected override object GetState()
                {
                    return this.NaturalKey;
                }

                protected override void SetState(object memento)
                {
                    this.NaturalKey = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.NaturalKey = @event.NaturalKey;
                }
            }

            public class NewSubject
            {
                public string NaturalKey { get; set; }
            }

            public class SubjectRepository : SqlServerRepository<Subject>
            {
                public SubjectRepository(string connectionString)
                    : base(connectionString)
                {
                }

                protected override void Save(Guid id, object memento, string oldState, out string newState)
                {
                    using (var connection = new SqlConnection(this.ConnectionString))
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = @"MERGE dbo.Subjects AS [Target]
USING (select '" + id.ToString() + "' as [Id], '" + memento.ToString() + "' as [NaturalKey], " + (string.IsNullOrEmpty(oldState) ? "NULL" : "'" + oldState + "'") + @" as [State]) AS [Source]
ON [Target].[Id] = [Source].[Id]
WHEN MATCHED AND [Target].[State] = [Source].[State] THEN  
  UPDATE SET 
    [Target].[NaturalKey] = [Source].[NaturalKey], 
    [Target].[State] = LEFT(CAST(NEWID() AS NVARCHAR(36)), 8)
WHEN NOT MATCHED AND [Source].[State] IS NULL THEN  
  INSERT ([Id], [NaturalKey], [State]) 
  VALUES ([Source].[Id], [Source].[NaturalKey], LEFT(CAST(NEWID() AS NVARCHAR(36)), 8))
OUTPUT [Inserted].[State];";

                        connection.Open();

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                throw new ConcurrencyException("Concurrency!");
                            }

                            newState = Convert.ToString(reader["State"]);
                        }
                    }
                }

                protected override object Load(Guid id, out string state)
                {
                    using (var connection = new SqlConnection(this.ConnectionString))
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = "SELECT [NaturalKey], [State] FROM [dbo].[Subjects] WHERE [Id] = '" + id.ToString() + "'";

                        connection.Open();

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                throw new AggregateRootNotFoundException("Not found!");
                            }

                            state = Convert.ToString(reader["State"]);
                            return Convert.ToString(reader["NaturalKey"]);
                        }
                    }
                }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>()
                        .ToUseNaturalKey(subject => subject.NaturalKey)
                        .ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
