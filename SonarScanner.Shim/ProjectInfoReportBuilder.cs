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
 
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarScanner.Shim
{
    /// <summary>
    /// Outputs a report summarising the project info files that were found.
    /// This is not used by SonarQube: it is only for debugging purposes.
    /// </summary>
    internal class ProjectInfoReportBuilder
    {
        private const string ReportFileName = "ProjectInfo.log";

        private readonly AnalysisConfig config;
        private readonly ProjectInfoAnalysisResult analysisResult;
        private readonly ILogger logger;

        private readonly StringBuilder sb;

        #region Public methods

        public static void WriteSummaryReport(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProjectInfoReportBuilder builder = new ProjectInfoReportBuilder(config, result, logger);
            builder.Generate();
        }

        #endregion

        #region Private methods

        private ProjectInfoReportBuilder(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            this.config = config;
            this.analysisResult = result;
            this.logger = logger;
            this.sb = new StringBuilder();
        }

        private void Generate()
        {
            IEnumerable<ProjectInfo> validProjects = this.analysisResult.GetProjectsByStatus(ProjectInfoValidity.Valid);

            WriteTitle(Resources.REPORT_ProductProjectsTitle);
            WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Product));
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_TestProjectsTitle);
            WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Test));
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_InvalidProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.DuplicateGuid);
            WriteFilesByStatus(ProjectInfoValidity.InvalidGuid);
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_SkippedProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.NoFilesToAnalyze);
            WriteGroupSpacer();

            WriteTitle(Resources.REPORT_ExcludedProjectsTitle);
            WriteFilesByStatus(ProjectInfoValidity.ExcludeFlagSet);
            WriteGroupSpacer();

            string reportFileName = Path.Combine(config.SonarOutputDir, ReportFileName);
            logger.LogDebug(Resources.MSG_WritingSummary, reportFileName);
            File.WriteAllText(reportFileName, sb.ToString());
        }

        private void WriteTitle(string title)
        {
            this.sb.AppendLine(title);
            this.sb.AppendLine("---------------------------------------");
        }

        private void WriteGroupSpacer()
        {
            this.sb.AppendLine();
            this.sb.AppendLine();
        }

        private void WriteFilesByStatus(params ProjectInfoValidity[] statuses)
        {
            IEnumerable<ProjectInfo> projects = Enumerable.Empty<ProjectInfo>();

            foreach (ProjectInfoValidity status in statuses)
            {
                projects = projects.Concat(this.analysisResult.GetProjectsByStatus(status));
            }

            if (!projects.Any())
            {
                this.sb.AppendLine(Resources.REPORT_NoProjectsOfType);
            }
            else
            {
                WriteFileList(projects);
            }
        }

        private void WriteFileList(IEnumerable<ProjectInfo> projects)
        {
            foreach(ProjectInfo project in projects)
            {
                this.sb.AppendLine(project.FullPath);
            }
        }

        #endregion
    }
}
