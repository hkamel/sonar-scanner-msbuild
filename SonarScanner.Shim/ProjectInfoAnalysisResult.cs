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
using System.Collections.Generic;
using System.Linq;

namespace SonarScanner.Shim
{
    public class ProjectInfoAnalysisResult
    {
        #region Public properties

        public IDictionary<ProjectInfo, ProjectInfoValidity> Projects { get; } = new Dictionary<ProjectInfo, ProjectInfoValidity>();

        public bool RanToCompletion { get; set; }

        public string FullPropertiesFilePath { get; set; }

        public ICollection<string> SharedFiles { get; } = new HashSet<string>();

        #endregion

        #region Public methods

        public IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoValidity status)
        {
            return this.Projects.Where(p => p.Value == status).Select(p => p.Key).ToArray();
        }

        #endregion

    }
}
