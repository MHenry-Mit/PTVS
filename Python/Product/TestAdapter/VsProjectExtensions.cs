﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.PythonTools.TestAdapter {
    internal static class VSProjectExtensions {
        /// <summary>
        /// Gets the name of the project.
        /// </summary>
        public static string GetProjectName(this IVsProject project) {
            ValidateArg.NotNull(project, "project");

            var projectHierarchy = (IVsHierarchy)project;
            object projectName;
            ErrorHandler.ThrowOnFailure(projectHierarchy.GetProperty((uint)VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out projectName));
            return (string)projectName;
        }

        private static string GetAggregateProjectTypeGuids(this IVsProject project) {
            ValidateArg.NotNull(project, "project");

            var aggregatableProject = project as IVsAggregatableProject;
            var aggregatableProjectCorrected = project as IVsAggregatableProjectCorrected;

            // Then it is not an aggregated project
            if (aggregatableProject == null && aggregatableProjectCorrected == null) {
                return string.Empty;
            }

            var projectTypeGuids = string.Empty;

            if (aggregatableProject != null) {
                ErrorHandler.ThrowOnFailure(aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids));
            } else if (aggregatableProjectCorrected != null) {
                ErrorHandler.ThrowOnFailure(aggregatableProjectCorrected.GetAggregateProjectTypeGuids(out projectTypeGuids));
            }

            return projectTypeGuids;
        }

        /// <summary>
        /// Returns whether the parameter project is a test project or not. 
        /// </summary>
        public static bool IsTestProject(this IVsProject project) {
            ValidateArg.NotNull(project, "project");

            string projectTypeGuids = project.GetAggregateProjectTypeGuids();

            // Currently we assume that all Python projects are test projects.
            return projectTypeGuids.IndexOf(PythonProjectGuidString, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Gets the project home directory.
        /// </summary>
        public static string GetProjectHome(this IVsProject project) {
            var hier = (IVsHierarchy)project;
            var proj = hier.GetProperty<EnvDTE.Project>(__VSHPROPID.VSHPROPID_ExtObject);
            return proj.Properties.Item("ProjectHome").Value as string;
        }

        /// <summary>
        /// Gets the file paths of items in the project.
        /// </summary>
        public static IEnumerable<string> GetProjectItemPaths(this IVsProject project) {
            string path;
            ErrorHandler.ThrowOnFailure(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out path));
            if (string.IsNullOrEmpty(path)) {
                yield break;
            }

            yield return path;
            foreach (var filePath in project.GetProjectItems()) {
                if (!string.IsNullOrEmpty(filePath)) {
                    yield return filePath;
                }
            }
        }

        /// <summary>
        /// Get the items present in the project
        /// </summary>
        public static IEnumerable<string> GetProjectItems(this IVsProject project) {
            Debug.Assert(project != null, "Project is not null");

            // Each item in VS OM is IVSHierarchy. 
            IVsHierarchy hierarchy = (IVsHierarchy)project;

            return GetProjectItems(hierarchy, VSConstants.VSITEMID_ROOT);
        }

        /// <summary>
        /// Get project items
        /// </summary>
        private static IEnumerable<string> GetProjectItems(IVsHierarchy project, uint itemId) {
            object pVar = GetPropertyValue((int)__VSHPROPID.VSHPROPID_FirstChild, itemId, project);

            uint childId = GetItemId(pVar);
            while (childId != VSConstants.VSITEMID_NIL) {
                foreach (string item in GetProjectItems(project, childId)) {
                    yield return item;
                }

                string childPath = GetCanonicalName(childId, project);
                yield return childPath;

                pVar = GetPropertyValue((int)__VSHPROPID.VSHPROPID_NextSibling, childId, project);
                childId = GetItemId(pVar);
            }
        }

        /// <summary>
        /// Convert parameter object to ItemId
        /// </summary>
        private static uint GetItemId(object pvar) {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }

        /// <summary>
        /// Get the parameter property value
        /// </summary>
        private static object GetPropertyValue(int propid, uint itemId, IVsHierarchy vsHierarchy) {
            if (itemId == VSConstants.VSITEMID_NIL) {
                return null;
            }

            object o;
            if (ErrorHandler.Failed(vsHierarchy.GetProperty(itemId, propid, out o))) {
                return null;
            }
            return o;
        }


        /// <summary>
        /// Get the canonical name
        /// </summary>
        private static string GetCanonicalName(uint itemId, IVsHierarchy hierarchy) {
            Debug.Assert(itemId != VSConstants.VSITEMID_NIL, "ItemId cannot be nill");

            string strRet = string.Empty;
            if (ErrorHandler.Failed(hierarchy.GetCanonicalName(itemId, out strRet))) {
                return string.Empty;
            }
            return strRet;
        }

        public const string PythonProjectGuidString = "{888888A0-9F3D-457C-B088-3A5042F75D52}";
    }
}