﻿/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0, written in C#. 
Copyright (C) 2011 Tao Klerks

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using NDesk.Options;

namespace PoorMansTSqlFormatterCmdLine
{
    class Program
    {
        private static FrameworkClassReplacements.SingleAssemblyResourceManager _generalResourceManager = null;

        const string UILANGUAGE_EN = "EN";
        const string UILANGUAGE_FR = "FR";
        const string UILANGUAGE_ES = "ES";

        static int Main(string[] args)
        {
            string indentString = "\t";
            int spacesPerTab = 4;
            int maxLineWidth = 999;
            bool trailingCommas = false;
            bool spaceAfterExpandedComma = false;
            bool expandBetweenConditions = true;
            bool expandBooleanExpressions = true;
            bool expandCaseStatements = true;
            bool expandCommaLists = true;
            bool breakJoinOnSections = false;
            bool uppercaseKeywords = true;
            bool standardizeKeywords = true;

            bool allowParsingErrors = false;
            bool showUsage = false;
            List<string> extensions = new List<string>();
            bool backups = true;
            bool recursiveSearch = false;
            string outputFileOrFolder = null;
            string uiLangCode = null;

            OptionSet p = new OptionSet()
              .Add("is|indentString=", delegate(string v) { indentString = v; })
              .Add("st|spacesPerTab=", delegate(string v) { spacesPerTab = int.Parse(v); })
              .Add("mw|maxLineWidth=", delegate(string v) { maxLineWidth = int.Parse(v); })
              .Add("tc|trailingCommas", delegate(string v) { trailingCommas = v != null; })
              .Add("sac|spaceAfterExpandedComma", delegate(string v) { spaceAfterExpandedComma = v != null; })
              .Add("ebc|expandBetweenConditions", delegate(string v) { expandBetweenConditions = v != null; })
              .Add("ebe|expandBooleanExpressions", delegate(string v) { expandBooleanExpressions = v != null; })
              .Add("ecs|expandCaseStatements", delegate(string v) { expandCaseStatements = v != null; })
              .Add("ecl|expandCommaLists", delegate(string v) { expandCommaLists = v != null; })
              .Add("bjo|breakJoinOnSections", delegate(string v) { breakJoinOnSections = v != null; })
              .Add("uk|uppercaseKeywords", delegate(string v) { uppercaseKeywords = v != null; })
              .Add("sk|standardizeKeywords", delegate(string v) { standardizeKeywords = v != null; })
              .Add("ae|allowParsingErrors", delegate(string v) { allowParsingErrors = v != null; })
              .Add("e|extensions=", delegate(string v) { extensions.Add((v.StartsWith(".") ? "" : ".") + v); })
              .Add("r|recursive", delegate(string v) { recursiveSearch = v != null; })
              .Add("b|backups", delegate(string v) { backups = v != null; })
              .Add("o|outputFileOrFolder=", delegate(string v) { outputFileOrFolder = v; })
              .Add("l|languageCode=", delegate(string v) { uiLangCode = v; })
              .Add("h|?|help", delegate(string v) { showUsage = v != null; })
                  ;

            //first parse the args
            List<string> remainingArgs = p.Parse(args);

            //then switch language if necessary
            if (uiLangCode != null)
            {
                uiLangCode = uiLangCode.ToUpper(System.Globalization.CultureInfo.InvariantCulture);
                if (!uiLangCode.Equals(UILANGUAGE_EN)
                    && !uiLangCode.Equals(UILANGUAGE_FR)
                    && !uiLangCode.Equals(UILANGUAGE_ES)
                    )
                {
                    showUsage = true;
                    //get the resource manager with default language, before displaying error.
                    _generalResourceManager = new FrameworkClassReplacements.SingleAssemblyResourceManager("GeneralLanguageContent", Assembly.GetExecutingAssembly(), typeof(Program));
                    Console.WriteLine(_generalResourceManager.GetString("UnrecognizedLanguageErrorMessage"));
                }
                else
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(uiLangCode);
                    _generalResourceManager = new FrameworkClassReplacements.SingleAssemblyResourceManager("GeneralLanguageContent", Assembly.GetExecutingAssembly(), typeof(Program));
                    //get the resource manager AFTER setting language as requested.
                }
            }
            else
            {
                _generalResourceManager = new FrameworkClassReplacements.SingleAssemblyResourceManager("GeneralLanguageContent", Assembly.GetExecutingAssembly(), typeof(Program));
            }

            //then complain about any unrecognized args
            if (remainingArgs.Count != 1)
            {
                showUsage = true;
                Console.WriteLine(_generalResourceManager.GetString("UnrecognizedArgumentsErrorMessage"));
            }


            if (extensions.Count == 0)
                extensions.Add(".sql");

            if (showUsage)
            {
                Console.WriteLine(_generalResourceManager.GetString("ProgramSummary"));
                Console.WriteLine("v" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
                Console.WriteLine(_generalResourceManager.GetString("ProgramUsageNotes"));
                return 1;
            }

            var formatter = new PoorMansTSqlFormatterLib.Formatters.TSqlStandardFormatter(
                indentString, 
                spacesPerTab, 
                maxLineWidth, 
                expandCommaLists, 
                trailingCommas, 
                spaceAfterExpandedComma, 
                expandBooleanExpressions, 
                expandCaseStatements,
                expandBetweenConditions,
                breakJoinOnSections,
                uppercaseKeywords, 
                false,
                standardizeKeywords
                );
            formatter.ErrorOutputPrefix = _generalResourceManager.GetString("ParseErrorWarningPrefix");
            var formattingManager = new PoorMansTSqlFormatterLib.SqlFormattingManager(formatter);

            string searchPattern = Path.GetFileName(remainingArgs[0]);
            string baseDirectoryName = Path.GetDirectoryName(remainingArgs[0]);
            if (baseDirectoryName.Length == 0)
            {
                baseDirectoryName = ".";
                if (searchPattern.Equals("."))
                    searchPattern = "";
            }
            System.IO.DirectoryInfo baseDirectory = null;
            System.IO.FileSystemInfo[] matchingObjects = null;
            try
            {
                baseDirectory = new System.IO.DirectoryInfo(baseDirectoryName);
                if (searchPattern.Length > 0)
                {
                    if (recursiveSearch)
                        matchingObjects = baseDirectory.GetFileSystemInfos(searchPattern);
                    else
                        matchingObjects = baseDirectory.GetFiles(searchPattern);
                }
                else
                {
                    if (recursiveSearch)
                        matchingObjects = baseDirectory.GetFileSystemInfos();
                    else
                        matchingObjects = new FileSystemInfo[0];
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format(_generalResourceManager.GetString("PathPatternErrorMessage"), e.Message));
                return 2;
            }

            System.IO.StreamWriter singleFileWriter = null;
            string replaceFromFolderPath = null;
            string replaceToFolderPath = null;
            if (!string.IsNullOrEmpty(outputFileOrFolder))
            {
                //ignore the backups setting - wouldn't make sense to back up the source files if we're 
                // writing to another file anyway...
                backups = false;

                if (Directory.Exists(outputFileOrFolder)
                    && (File.GetAttributes(outputFileOrFolder) & FileAttributes.Directory) == FileAttributes.Directory
                    )
                {
                    replaceFromFolderPath = baseDirectory.FullName;
                    replaceToFolderPath = new DirectoryInfo(outputFileOrFolder).FullName;
                }
                else
                {
                    try
                    {
                        //let's not worry too hard about releasing this resource - this is a command-line program, 
                        // when it ends or dies all will be released anyway.
                        singleFileWriter = new StreamWriter(outputFileOrFolder);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(string.Format(_generalResourceManager.GetString("OutputFileCreationErrorMessage"), e.Message));
                        return 3;
                    }
                }
            }

            bool warningEncountered = false;
            if (!ProcessSearchResults(extensions, backups, allowParsingErrors, formattingManager, matchingObjects, singleFileWriter, replaceFromFolderPath, replaceToFolderPath, ref warningEncountered))
            {
                Console.WriteLine(string.Format(_generalResourceManager.GetString("NoFilesFoundWarningMessage"), remainingArgs[0], string.Join(",", extensions.ToArray())));
                return 4;
            }

            if (singleFileWriter != null)
            {
                singleFileWriter.Flush();
                singleFileWriter.Close();
                singleFileWriter.Dispose();
            }

            if (warningEncountered)
                return 5; //general "there were warnings" return code
            else
                return 0; //we got there, did something, and received no (handled) errors!
        }

        private static bool ProcessSearchResults(List<string> extensions, bool backups, bool allowParsingErrors, PoorMansTSqlFormatterLib.SqlFormattingManager formattingManager, FileSystemInfo[] matchingObjects, StreamWriter singleFileWriter, string replaceFromFolderPath, string replaceToFolderPath, ref bool warningEncountered)
        {
            bool fileFound = false;

            foreach (var fsEntry in matchingObjects)
            {
                if (fsEntry is FileInfo)
                {
                    if (extensions.Contains(fsEntry.Extension))
                    {
                        ReFormatFile((FileInfo)fsEntry, formattingManager, backups, allowParsingErrors, singleFileWriter, replaceFromFolderPath, replaceToFolderPath, ref warningEncountered);
                        fileFound = true;
                    }
                }
                else
                {
                    if (ProcessSearchResults(extensions, backups, allowParsingErrors, formattingManager, ((System.IO.DirectoryInfo)fsEntry).GetFileSystemInfos(), singleFileWriter, replaceFromFolderPath, replaceToFolderPath, ref warningEncountered))
                        fileFound = true;
                }
            }

            return fileFound;
        }

        private static void ReFormatFile(FileInfo fileInfo, PoorMansTSqlFormatterLib.SqlFormattingManager formattingManager, bool backups, bool allowParsingErrors, StreamWriter singleFileWriter, string replaceFromFolderPath, string replaceToFolderPath, ref bool warningEncountered)
        {
            bool failedBackup = false;
            string oldFileContents = "";
            string newFileContents = "";
            bool parsingError = false;
            bool failedFolder = false;
            Exception parseException = null;

            //TODO: play with / test encoding complexities
            //TODO: consider using auto-detection - read binary, autodetect, convert.
            //TODO: consider whether to keep same output encoding as source file, or always use same, and if so whether to make parameter-based.
            try
            {
                oldFileContents = System.IO.File.ReadAllText(fileInfo.FullName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(_generalResourceManager.GetString("FileReadFailureWarningMessage"), fileInfo.FullName));
                Console.WriteLine(string.Format(_generalResourceManager.GetString("ErrorDetailMessageFragment"), ex.Message));
                warningEncountered = true;
            }
            if (oldFileContents.Length > 0)
            {
                try
                {
                    newFileContents = formattingManager.Format(oldFileContents, ref parsingError);

                    //hide any handled parsing issues if they were requested to be allowed
                    if (allowParsingErrors) parsingError = false;
                }
                catch (Exception ex)
                {
                    parseException = ex;
                    parsingError = true;
                }

                if (parsingError)
                {
                    Console.WriteLine(string.Format(_generalResourceManager.GetString("ParsingErrorWarningMessage"), fileInfo.FullName));
                    if (parseException != null)
                        Console.WriteLine(string.Format(_generalResourceManager.GetString("ErrorDetailMessageFragment"), parseException.Message));
                    warningEncountered = true;
                }
            }
            if (!parsingError
                && (
                        (newFileContents.Length > 0 
                        && !oldFileContents.Equals(newFileContents)
                        )
                        || singleFileWriter != null
                        || (replaceFromFolderPath != null && replaceToFolderPath != null)
                    )
                )

            {
                if (backups)
                {
                    try
                    {
                        fileInfo.CopyTo(fileInfo.FullName + ".bak", true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format(_generalResourceManager.GetString("BackupFailureWarningMessage"), fileInfo.FullName, Environment.NewLine));
                        Console.WriteLine(string.Format(_generalResourceManager.GetString("ErrorDetailMessageFragment"), ex.Message));
                        failedBackup = true;
                        warningEncountered = true;
                    }
                }
                if (!failedBackup)
                {
                    if (singleFileWriter != null)
                    {
                        //we'll assume that running out of disk space, and other while-you-are-writing errors, and not worth worrying about
                        singleFileWriter.WriteLine(newFileContents);
                        singleFileWriter.WriteLine("GO");
                    }
                    else
                    {
                        string fullTargetPath = fileInfo.FullName;
                        if (replaceFromFolderPath != null && replaceToFolderPath != null)
                        {
                            fullTargetPath = fullTargetPath.Replace(replaceFromFolderPath, replaceToFolderPath);

                            string targetFolder = Path.GetDirectoryName(fullTargetPath);
                            try
                            {
                                if (!Directory.Exists(targetFolder))
                                    Directory.CreateDirectory(targetFolder);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(string.Format(_generalResourceManager.GetString("FolderCreationFailureWarningMessage"), targetFolder));
                                Console.WriteLine(string.Format(_generalResourceManager.GetString("ErrorDetailMessageFragment"), ex.Message));
                                failedFolder = true;
                                warningEncountered = true;
                            }
                        }

                        if (!failedFolder)
                        {
                            try
                            {
                                File.WriteAllText(fullTargetPath, newFileContents);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(string.Format(_generalResourceManager.GetString("ContentWriteFailureWarningMessage"), fileInfo.FullName));
                                Console.WriteLine(string.Format(_generalResourceManager.GetString("ErrorDetailMessageFragment"), ex.Message));
                                if (replaceFromFolderPath == null || replaceToFolderPath == null)
                                    Console.WriteLine(_generalResourceManager.GetString("PossiblePartialWriteWarningMessage"));
                                warningEncountered = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
