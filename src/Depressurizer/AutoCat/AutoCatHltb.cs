﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using Depressurizer.Helpers;
using Depressurizer.Models;

namespace Depressurizer
{
    public enum TimeType
    {
        Main,

        Extras,

        Completionist
    }

    public class Hltb_Rule
    {
        #region Constructors and Destructors

        public Hltb_Rule(string name, float minHours, float maxHours, TimeType timeType)
        {
            Name = name;
            MinHours = minHours;
            MaxHours = maxHours;
            TimeType = timeType;
        }

        public Hltb_Rule(Hltb_Rule other)
        {
            Name = other.Name;
            MinHours = other.MinHours;
            MaxHours = other.MaxHours;
            TimeType = other.TimeType;
        }

        //XmlSerializer requires a parameterless constructor
        private Hltb_Rule() { }

        #endregion

        #region Public Properties

        public float MaxHours { get; set; }

        public float MinHours { get; set; }

        [XmlElement("Text")]
        public string Name { get; set; }

        public TimeType TimeType { get; set; }

        #endregion
    }

    public class AutoCatHltb : AutoCat
    {
        #region Constants

        public const string TypeIdString = "AutoCatHltb";

        public const string XmlName_Name = "Name", XmlName_Filter = "Filter", XmlName_Prefix = "Prefix", XmlName_IncludeUnknown = "IncludeUnknown", XmlName_UnknownText = "UnknownText", XmlName_Rule = "Rule", XmlName_Rule_Text = "Text", XmlName_Rule_MinHours = "MinHours", XmlName_Rule_MaxHours = "MaxHours", XmlName_Rule_TimeType = "TimeType";

        #endregion

        #region Fields

        [XmlElement("Rule")]
        public List<Hltb_Rule> Rules;

        #endregion

        #region Constructors and Destructors

        public AutoCatHltb(string name, string filter = null, string prefix = null, bool includeUnknown = true, string unknownText = "", List<Hltb_Rule> rules = null, bool selected = false) : base(name)
        {
            Filter = filter;
            Prefix = prefix;
            IncludeUnknown = includeUnknown;
            UnknownText = unknownText;
            Rules = rules == null ? new List<Hltb_Rule>() : rules;
            Selected = selected;
        }

        public AutoCatHltb(AutoCatHltb other) : base(other)
        {
            Filter = other.Filter;
            Prefix = other.Prefix;
            IncludeUnknown = other.IncludeUnknown;
            UnknownText = other.UnknownText;
            Rules = other.Rules.ConvertAll(rule => new Hltb_Rule(rule));
            Selected = other.Selected;
        }

        //XmlSerializer requires a parameterless constructor
        private AutoCatHltb() { }

        #endregion

        #region Public Properties

        public override AutoCatType AutoCatType => AutoCatType.Hltb;

        public bool IncludeUnknown { get; set; }

        public string Prefix { get; set; }

        public string UnknownText { get; set; }

        #endregion

        #region Properties

        private static Logger Logger => Logger.Instance;

        #endregion

        #region Public Methods and Operators

        public static AutoCatHltb LoadFromXmlElement(XmlElement xElement)
        {
            string name = XmlUtil.GetStringFromNode(xElement[XmlName_Name], TypeIdString);
            string filter = XmlUtil.GetStringFromNode(xElement[XmlName_Filter], null);
            string prefix = XmlUtil.GetStringFromNode(xElement[XmlName_Prefix], string.Empty);
            bool includeUnknown = XmlUtil.GetBoolFromNode(xElement[XmlName_IncludeUnknown], false);
            string unknownText = XmlUtil.GetStringFromNode(xElement[XmlName_UnknownText], string.Empty);

            List<Hltb_Rule> rules = new List<Hltb_Rule>();
            foreach (XmlNode node in xElement.SelectNodes(XmlName_Rule))
            {
                string ruleName = XmlUtil.GetStringFromNode(node[XmlName_Rule_Text], string.Empty);
                float ruleMin = XmlUtil.GetFloatFromNode(node[XmlName_Rule_MinHours], 0);
                float ruleMax = XmlUtil.GetFloatFromNode(node[XmlName_Rule_MaxHours], 0);
                string type = XmlUtil.GetStringFromNode(node[XmlName_Rule_TimeType], string.Empty);
                TimeType ruleTimeType;
                switch (type)
                {
                    case "Extras":
                        ruleTimeType = TimeType.Extras;
                        break;
                    case "Completionist":
                        ruleTimeType = TimeType.Completionist;
                        break;
                    default:
                        ruleTimeType = TimeType.Main;
                        break;
                }

                rules.Add(new Hltb_Rule(ruleName, ruleMin, ruleMax, ruleTimeType));
            }

            AutoCatHltb result = new AutoCatHltb(name, filter, prefix, includeUnknown, unknownText)
            {
                Rules = rules
            };
            return result;
        }

        public override AutoCatResult CategorizeGame(GameInfo game, Filter filter)
        {
            if (games == null)
            {
                Logger.Error(GlobalStrings.Log_AutoCat_GamelistNull);
                throw new ApplicationException(GlobalStrings.AutoCatGenre_Exception_NoGameList);
            }

            if (db == null)
            {
                Logger.Error(GlobalStrings.Log_AutoCat_DBNull);
                throw new ApplicationException(GlobalStrings.AutoCatGenre_Exception_NoGameDB);
            }

            if (game == null)
            {
                Logger.Error(GlobalStrings.Log_AutoCat_GameNull);
                return AutoCatResult.Failure;
            }

            if (!db.Contains(game.Id, out DatabaseEntry entry))
            {
                return AutoCatResult.NotInDatabase;
            }

            if (!game.IncludeGame(filter))
            {
                return AutoCatResult.Filtered;
            }

            string result = null;

            float hltbMain = entry.HltbMain / 60.0f;
            float hltbExtras = entry.HltbExtras / 60.0f;
            float hltbCompletionist = entry.HltbCompletionist / 60.0f;

            if (IncludeUnknown && hltbMain == 0.0f && hltbExtras == 0.0f && hltbCompletionist == 0.0f)
            {
                result = UnknownText;
            }
            else
            {
                foreach (Hltb_Rule rule in Rules)
                {
                    if (CheckRule(rule, hltbMain, hltbExtras, hltbCompletionist))
                    {
                        result = rule.Name;
                        break;
                    }
                }
            }

            if (result != null)
            {
                result = GetProcessedString(result);
                game.AddCategory(games.GetCategory(result));
            }

            return AutoCatResult.Success;
        }

        public override AutoCat Clone()
        {
            return new AutoCatHltb(this);
        }

        public override void WriteToXml(XmlWriter writer)
        {
            writer.WriteStartElement(TypeIdString);

            writer.WriteElementString(XmlName_Name, Name);
            if (Filter != null)
            {
                writer.WriteElementString(XmlName_Filter, Filter);
            }

            if (Prefix != null)
            {
                writer.WriteElementString(XmlName_Prefix, Prefix);
            }

            writer.WriteElementString(XmlName_IncludeUnknown, IncludeUnknown.ToString().ToLowerInvariant());
            writer.WriteElementString(XmlName_UnknownText, UnknownText);

            foreach (Hltb_Rule rule in Rules)
            {
                writer.WriteStartElement(XmlName_Rule);
                writer.WriteElementString(XmlName_Rule_Text, rule.Name);
                writer.WriteElementString(XmlName_Rule_MinHours, rule.MinHours.ToString());
                writer.WriteElementString(XmlName_Rule_MaxHours, rule.MaxHours.ToString());
                writer.WriteElementString(XmlName_Rule_TimeType, rule.TimeType.ToString());

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        #endregion

        #region Methods

        private bool CheckRule(Hltb_Rule rule, float hltbMain, float hltbExtras, float hltbCompletionist)
        {
            float hours = 0.0f;
            if (rule.TimeType == TimeType.Main)
            {
                hours = hltbMain;
            }
            else if (rule.TimeType == TimeType.Extras)
            {
                hours = hltbExtras;
            }
            else if (rule.TimeType == TimeType.Completionist)
            {
                hours = hltbCompletionist;
            }

            if (hours == 0.0f)
            {
                return false;
            }

            return hours >= rule.MinHours && (hours <= rule.MaxHours || rule.MaxHours == 0.0f);
        }

        private string GetProcessedString(string s)
        {
            if (!string.IsNullOrEmpty(Prefix))
            {
                return Prefix + s;
            }

            return s;
        }

        #endregion
    }
}
