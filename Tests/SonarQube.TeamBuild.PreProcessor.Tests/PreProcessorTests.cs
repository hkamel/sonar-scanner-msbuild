﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Setup

        #endregion

        #region Tests

        [TestMethod]
        public void PreProc_InvalidArgs()
        {
            // Arrange
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            TeamBuildPreProcessor preprocessor = new TeamBuildPreProcessor(
                new MockObjectFactory(mockServer, new Mock<ITargetsInstaller>().Object, new MockRoslynAnalyzerProvider(), new Mock<IRulesetGenerator>().Object),
                new TestLogger());

            // Act and assert
            AssertException.Expects<ArgumentNullException>(() => preprocessor.Execute(null));
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rulesets are generated
            // * config file is created

            // Arrange
            string workingDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Configure the server
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            ServerDataModel data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("key")
                .AddRule(new ActiveRule("fxcop", "cs.rule1"))
                .AddRule(new ActiveRule("fxcop", "cs.rule2"))
                .AddRule(new ActiveRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("key")
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule2"))
                .AddRule(new ActiveRule("vbnet", "vb.rule3"));

            MockRoslynAnalyzerProvider mockAnalyzerProvider = new MockRoslynAnalyzerProvider();
            mockAnalyzerProvider.SettingsToReturn = new AnalyzerSettings();
            mockAnalyzerProvider.SettingsToReturn.RuleSetFilePath = "c:\\xxx.ruleset";

            Mock<ITargetsInstaller> mockTargetsInstaller = new Mock<ITargetsInstaller>();
            MockObjectFactory mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider, new RulesetGenerator());

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                bool success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0"));
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(logger, workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetInactiveRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);

            string fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, string.Format(TeamBuildPreProcessor.FxCopRulesetName, TeamBuildPreProcessor.CSharpLanguage));
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "cs.rule1", "cs.rule2");

            fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, string.Format(TeamBuildPreProcessor.FxCopRulesetName, TeamBuildPreProcessor.VBNetLanguage));
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "vb.rule1", "vb.rule2");
        }

        [TestMethod]
        public void PreProc_EndToEnd_SuccessCase_With_Organization()
        {
            // Checks end-to-end happy path for the pre-processor i.e.
            // * arguments are parsed
            // * targets are installed
            // * server properties are fetched
            // * rulesets are generated
            // * config file is created

            // Arrange
            string workingDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Configure the server
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            ServerDataModel data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", "organization")
                .AddProject("key")
                .AddRule(new ActiveRule("fxcop", "cs.rule1"))
                .AddRule(new ActiveRule("fxcop", "cs.rule2"))
                .AddRule(new ActiveRule("csharpsquid", "cs.rule3"));

            data.AddQualityProfile("qp2", "vbnet", "organization")
                .AddProject("key")
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule2"))
                .AddRule(new ActiveRule("vbnet", "vb.rule3"));

            MockRoslynAnalyzerProvider mockAnalyzerProvider = new MockRoslynAnalyzerProvider();
            mockAnalyzerProvider.SettingsToReturn = new AnalyzerSettings();
            mockAnalyzerProvider.SettingsToReturn.RuleSetFilePath = "c:\\xxx.ruleset";

            Mock<ITargetsInstaller> mockTargetsInstaller = new Mock<ITargetsInstaller>();
            MockObjectFactory mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider, new RulesetGenerator());

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                bool success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", "organization"));
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(logger, workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetInactiveRules", 2); // C# and VBNet

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 2, logger);

            string fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, string.Format(TeamBuildPreProcessor.FxCopRulesetName, TeamBuildPreProcessor.CSharpLanguage));
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "cs.rule1", "cs.rule2");

            fxCopFilePath = AssertFileExists(settings.SonarConfigDirectory, string.Format(TeamBuildPreProcessor.FxCopRulesetName, TeamBuildPreProcessor.VBNetLanguage));
            PreProcessAsserts.AssertRuleSetContainsRules(fxCopFilePath, "vb.rule1", "vb.rule2");
        }

        [TestMethod]
        public void PreProc_NoPlugin()
        {
            // Arrange
            string workingDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Configure the server
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            ServerDataModel data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("invalid_plugin");

            MockRoslynAnalyzerProvider mockAnalyzerProvider = new MockRoslynAnalyzerProvider();
            mockAnalyzerProvider.SettingsToReturn = new AnalyzerSettings();
            mockAnalyzerProvider.SettingsToReturn.RuleSetFilePath = "c:\\xxx.ruleset";

            Mock<ITargetsInstaller> mockTargetsInstaller = new Mock<ITargetsInstaller>();
            MockObjectFactory mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider, new RulesetGenerator());

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                bool success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0"));
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(logger, workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 0); // No valid plugin
            mockServer.AssertMethodCalled("GetActiveRules", 0); // No valid plugin
            mockServer.AssertMethodCalled("GetInactiveRules", 0); // No valid plugin

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or aditional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        [TestMethod]
        public void PreProc_NoProject()
        {
            // Arrange
            string workingDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            // Configure the server
            MockSonarQubeServer mockServer = new MockSonarQubeServer();

            ServerDataModel data = mockServer.Data;
            data.ServerProperties.Add("server.key", "server value 1");

            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            data.AddQualityProfile("qp1", "cs", null)
                .AddProject("invalid")
                .AddRule(new ActiveRule("fxcop", "cs.rule1"))
                .AddRule(new ActiveRule("fxcop", "cs.rule2"));

            data.AddQualityProfile("qp2", "vbnet", null)
                .AddProject("invalid")
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule1"))
                .AddRule(new ActiveRule("fxcop-vbnet", "vb.rule2"));

            MockRoslynAnalyzerProvider mockAnalyzerProvider = new MockRoslynAnalyzerProvider();
            mockAnalyzerProvider.SettingsToReturn = new AnalyzerSettings();
            mockAnalyzerProvider.SettingsToReturn.RuleSetFilePath = "c:\\xxx.ruleset";

            Mock<ITargetsInstaller> mockTargetsInstaller = new Mock<ITargetsInstaller>();
            MockObjectFactory mockFactory = new MockObjectFactory(mockServer, mockTargetsInstaller.Object, mockAnalyzerProvider, new RulesetGenerator());

            TeamBuildSettings settings;
            using (PreprocessTestUtils.CreateValidNonTeamBuildScope())
            using (new WorkingDirectoryScope(workingDir))
            {
                settings = TeamBuildSettings.GetSettingsFromEnvironment(new TestLogger());
                Assert.IsNotNull(settings, "Test setup error: TFS environment variables have not been set correctly");
                Assert.AreEqual(BuildEnvironment.NotTeamBuild, settings.BuildEnvironment, "Test setup error: build environment was not set correctly");

                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(mockFactory, logger);

                // Act
                bool success = preProcessor.Execute(CreateValidArgs("key", "name", "1.0", null));
                Assert.IsTrue(success, "Expecting the pre-processing to complete successfully");
            }

            // Assert
            AssertDirectoriesCreated(settings);

            mockTargetsInstaller.Verify(x => x.InstallLoaderTargets(logger, workingDir), Times.Once());
            mockServer.AssertMethodCalled("GetProperties", 1);
            mockServer.AssertMethodCalled("GetAllLanguages", 1);
            mockServer.AssertMethodCalled("TryGetQualityProfile", 2); // C# and VBNet
            mockServer.AssertMethodCalled("GetActiveRules", 0); // no quality profile assigned to project
            mockServer.AssertMethodCalled("GetInactiveRules", 0);

            AssertAnalysisConfig(settings.AnalysisConfigFilePath, 0, logger);

            // only contains SonarQubeAnalysisConfig (no rulesets or aditional files)
            AssertDirectoryContains(settings.SonarConfigDirectory, Path.GetFileName(settings.AnalysisConfigFilePath));
        }

        #endregion Tests

        #region Setup

        string[] CreateValidArgs(string projectKey, string projectName, string projectVersion)
        {
            return new string[] {
                "/k:" + projectKey, "/n:" + projectName, "/v:" + projectVersion,
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"};
        }

        string[] CreateValidArgs(string projectKey, string projectName, string projectVersion, string organization)
        {
            return new string[] {
                "/k:" + projectKey, "/n:" + projectName, "/v:" + projectVersion, "/o:" + organization,
                "/d:cmd.line1=cmdline.value.1",
                "/d:sonar.host.url=http://host",
                "/d:sonar.log.level=INFO|DEBUG"};
        }
        
        #endregion

        #region Checks

        private void AssertDirectoriesCreated(TeamBuildSettings settings)
        {
            AssertDirectoryExists(settings.AnalysisBaseDirectory);
            AssertDirectoryExists(settings.SonarConfigDirectory);
            AssertDirectoryExists(settings.SonarOutputDirectory);
            // The bootstrapper is responsible for creating the bin directory
        }

        private void AssertAnalysisConfig(string filePath, int noAnalyzers, TestLogger logger)
        {
            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
            logger.AssertVerbosity(LoggerVerbosity.Debug);

            AssertConfigFileExists(filePath);
            AnalysisConfig actualConfig = AnalysisConfig.Load(filePath);

            Assert.AreEqual("key", actualConfig.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual("name", actualConfig.SonarProjectName, "Unexpected project name");
            Assert.AreEqual("1.0", actualConfig.SonarProjectVersion, "Unexpected project version");

            Assert.IsNotNull(actualConfig.AnalyzersSettings, "Analyzer settings should not be null");
            Assert.AreEqual(actualConfig.AnalyzersSettings.Count, noAnalyzers);

            AssertExpectedLocalSetting(SonarProperties.HostUrl, "http://host", actualConfig);
            AssertExpectedLocalSetting("cmd.line1", "cmdline.value.1", actualConfig);
            AssertExpectedServerSetting("server.key", "server value 1", actualConfig);
        }

        private void AssertConfigFileExists(string filePath)
        {
            Assert.IsTrue(File.Exists(filePath), "Expecting the analysis config file to exist. Path: {0}", filePath);
            this.TestContext.AddResultFile(filePath);
        }

        private void AssertDirectoryContains(string dirPath, params string[] fileNames)
        {
            Directory.Exists(dirPath);
            IEnumerable<string> actualFileNames = Directory.GetFiles(dirPath).Select(f => Path.GetFileName(f));
            CollectionAssert.AreEqual(fileNames, actualFileNames.ToArray());
        }

        private static void AssertExpectedLocalSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            Property actualProperty;
            bool found = Property.TryGetProperty(key, actualConfig.LocalSettings, out actualProperty);

            Assert.IsTrue(found, "Failed to find the expected local setting: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertExpectedServerSetting(string key, string expectedValue, AnalysisConfig actualConfig)
        {
            Property actualProperty;
            bool found = Property.TryGetProperty(key, actualConfig.ServerSettings, out actualProperty);

            Assert.IsTrue(found, "Failed to find the expected server setting: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Unexpected property value. Key: {0}", key);
        }

        private static void AssertDirectoryExists(string path)
        {
            Assert.IsTrue(Directory.Exists(path), "Expected directory does not exist: {0}", path);
        }

        private static string AssertFileExists(string directory, string fileName)
        {
            string fullPath = Path.Combine(directory, fileName);
            Assert.IsTrue(File.Exists(fullPath), "Expected file does not exist");
            return fullPath;
        }

        #endregion
    }
}