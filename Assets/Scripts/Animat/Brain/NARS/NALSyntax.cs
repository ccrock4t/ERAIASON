/*
    Author: Christian Hahm
    Created: October 9, 2020
    Purpose: Defines the syntax to be used for Narsese
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

public enum StatementSyntax {

    [Description("(")] Start,
    [Description(")")] End,
    [Description("%")] TruthValMarker,
    [Description("#")] ExpectationMarker,
    [Description(";")] ValueSeparator,
    [Description(",")] TermDivider,
    [Description("$")] BudgetMarker,
    [Description("_")] ImagePlaceHolder,

}

public enum Tense {

    [Description(":/:")] Future,
    [Description(@":\:")] Past,
    [Description(":|:")] Present,
    [Description("")] Eternal
}


public enum TermConnector {
    [Description("empty")] EmptyConnector,

    // NAL-2
    [Description("{")] ExtensionalSetStart,
    [Description("}")] ExtensionalSetEnd,
    [Description("[")] IntensionalSetStart,
    [Description("]")] IntensionalSetEnd,

    // NAL-3
    [Description("&")] ExtensionalIntersection,
    [Description("|")] IntensionalIntersection,
    [Description("-")] ExtensionalDifference,
    [Description("~")] IntensionalDifference,

    // NAL-4
    [Description("*")] Product,
    [Description("/")] ExtensionalImage,
    [Description(@"\")] IntensionalImage,
    [Description("_")] ImagePlaceHolder,

    // NAL-5
    [Description("--")] Negation,
    [Description("&&")] Conjunction,
    [Description("||")] Disjunction,
    [Description("&/")] SequentialConjunction,
    [Description("&|")] ParallelConjunction
}



public static class TermConnectorMethods
{

    public static bool is_first_order(TermConnector? connector)
    {
        /*
            First order connectors are Term Connectors
            Higher order connectors are Statement Connectors
        */
        return !(connector == TermConnector.Negation ||
                    connector == TermConnector.Conjunction ||
                    connector == TermConnector.Disjunction ||
                    connector == TermConnector.SequentialConjunction ||
                    connector == TermConnector.ParallelConjunction);
    }


    public static bool is_order_invariant(TermConnector? connector)
    {
        return (connector == TermConnector.ExtensionalIntersection ||
                connector == TermConnector.IntensionalIntersection ||
                connector == TermConnector.ExtensionalSetStart ||
                connector == TermConnector.IntensionalSetStart ||
                connector == TermConnector.Negation ||
                connector == TermConnector.Conjunction ||
                connector == TermConnector.Disjunction);
    }


    public static bool is_conjunction(TermConnector? connector) {
        //assert connector == not null, "ERROR: null == not a term connector"
        return (connector == TermConnector.Conjunction ||
                connector == TermConnector.SequentialConjunction ||
                connector == TermConnector.ParallelConjunction);
    }


    public static bool contains_conjunction(string str) {

        return str.Contains(SyntaxUtils.stringValueOf(TermConnector.Conjunction)) ||
                str.Contains(SyntaxUtils.stringValueOf(TermConnector.SequentialConjunction)) ||
                str.Contains(SyntaxUtils.stringValueOf(TermConnector.ParallelConjunction));
    }


    public static bool contains_higher_level_connector(string str) {

        string[] names = Enum.GetNames(typeof(TermConnector));
        foreach (string name in names)
        {
            TermConnector connector = (TermConnector)SyntaxUtils.enumValueOf<TermConnector>(name);
            if (!is_first_order(connector)) {
                // higher order connector
                if (str.Contains(name)) return true;
            }
        }

        return false;

    }


    public static TermConnector get_set_end_connector_from_set_start_connector(TermConnector start_connector) {
        if (start_connector == TermConnector.ExtensionalSetStart) return TermConnector.ExtensionalSetEnd;
        if (start_connector == TermConnector.IntensionalSetStart) return TermConnector.IntensionalSetEnd;
        Asserts.assert(false, "ERROR: Invalid start connector");
        return start_connector;
    }



    public static bool is_set_bracket_start(string bracketChar) {
        /*
        Returns true if character is a starting bracket for a set
        :param bracket:
        :return:
        */
        TermConnector? bracketConnector = (TermConnector?)SyntaxUtils.enumValueOf<TermConnector>(bracketChar);
        return (bracketConnector == TermConnector.IntensionalSetStart) ||
                (bracketConnector == TermConnector.ExtensionalSetStart);
    }


    public static bool is_set_bracket_end(string bracketChar)
    {
        /*
        Returns true if character is an ending bracket for a set
        :param bracket:
        :return:
        */
        TermConnector? bracketConnector = (TermConnector?)SyntaxUtils.enumValueOf<TermConnector>(bracketChar);
        return (bracketConnector == TermConnector.IntensionalSetEnd) ||
                (bracketConnector == TermConnector.ExtensionalSetEnd);
    }
}


public enum Copula {
    // Primary copula
    [Description("-->")] Inheritance,
    [Description("<->")] Similarity,
    [Description("==>")] Implication,
    [Description("<=>")] Equivalence,

    // Secondary copula
    [Description(":--")] Instance,
    [Description("--]")] Property,
    [Description(":-]")] InstanceProperty,
    [Description("=/>")] PredictiveImplication,
    [Description(@"=\>")] RetrospectiveImplication,
    [Description("=|>")] ConcurrentImplication,
    [Description("</>")] PredictiveEquivalence,
    [Description("<|>")] ConcurrentEquivalence
}

public static class CopulaMethods
{
    public static bool is_implication(Copula copula)
    {
        return copula == Copula.Implication ||
            copula == Copula.PredictiveImplication ||
            copula == Copula.RetrospectiveImplication;
    }


    public static bool is_first_order(Copula copula) {
        return copula == Copula.Inheritance ||
               copula == Copula.Similarity ||
               copula == Copula.Instance ||
               copula == Copula.Property ||
               copula == Copula.InstanceProperty;
    }


    public static bool is_temporal(Copula copula)
    {
        return copula == Copula.PredictiveImplication ||
               copula == Copula.RetrospectiveImplication ||
               copula == Copula.ConcurrentImplication ||
               copula == Copula.PredictiveEquivalence ||
               copula == Copula.ConcurrentEquivalence;
    }


    public static bool is_symmetric(Copula copula) {
        return copula == Copula.Similarity ||
               copula == Copula.Equivalence ||
               copula == Copula.PredictiveEquivalence ||
               copula == Copula.ConcurrentEquivalence;
    }


    public static bool is_string_a_copula(string value) {
        return SyntaxUtils.enumValueOf<Copula>(value) != null;
    }


    public static bool contains_copula(string str) {
        string[] names = Enum.GetNames(typeof(Copula));
        foreach (string name in names)
        {
            TermConnector connector = (TermConnector)SyntaxUtils.enumValueOf<TermConnector>(name);
            // higher order connector
            if (str.Contains(name)) return true;
        }

        return false;
    }


    public static bool contains_top_level_copula(string str) {
        (Copula? copula, int copulaIdx) = CopulaMethods.get_top_level_copula(str);
        return copula != null;
     }
 

    public static (Copula?, int) get_top_level_copula(string str){
        /*
             Searches for top - level copula in the string.

            :returns copula && index if it exists,
            :returns none && -1 otherwise
         */
        Copula? copula = null;
        int copula_idx = -1;

        int depth = 0;
        string statementStartString = SyntaxUtils.stringValueOf(StatementSyntax.Start);
        string statementEndString = SyntaxUtils.stringValueOf(StatementSyntax.End);

        for(int i = 0; i < str.Length; i++)
        {
            string v =  Char.ToString(str[i]);
            if (v == statementStartString)
            {
                depth += 1;
            }
            else if (v == statementEndString)
            {
                depth -= 1;
            }
            else if(depth == 1 && i + 3 <= str.Length && CopulaMethods.is_string_a_copula(str[i..(i + 3)])){
                copula_idx = i;
                string copula_string = str[i..(i + 3)];
                copula = (Copula)SyntaxUtils.enumValueOf<Copula>(copula_string);
            }
        }


        return (copula, copula_idx);
     }
 }



public enum Punctuation {
    [Description(".")] Judgment,
    [Description("?")] Question, // on truth-value
    [Description("!")] Goal,
    [Description("`")] Quest  // on desire-value #todo, decide value for Quest since @ == used for array now
}

public static class PunctuationMethods
{

    public static bool is_punctuation(string value)
    {
        return SyntaxUtils.enumValueOf<Punctuation>(value) != null;
    }


    public static Punctuation get_punctuation_from_string(string value)
    {
        return (Punctuation)SyntaxUtils.enumValueOf<Punctuation>(value);
    }

    public static string get_string_from_punctuation(Punctuation value)
    {
        FieldInfo fi = value.GetType().GetField(value.ToString());

        DescriptionAttribute[] attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];

        if (attributes != null && attributes.Any())
        {
            return attributes.First().Description;
        }

        return value.ToString();
    }
}

public static class NALSyntax
{
    /*
    List of valid characters that can be used in a term.
    */
    public static HashSet<char> valid_term_chars = new HashSet<char>(){
    'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
    'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'J', 'L', 'M',
    'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '^' };

    /*
    ID markers
    */
    public static string MARKER_ITEM_ID = "ItemID:";  // there are Sentence IDs and Bag Item IDs
    public static string MARKER_SENTENCE_ID = "SentenceID:";
    public static string MARKER_ID_END = ":ID ";
}


public static class SyntaxUtils
{
    public static Term image_place_holder_term = new AtomicTerm(stringValueOf(StatementSyntax.ImagePlaceHolder));
    public static bool is_valid_term(string term_string)
    {
        foreach (char c in term_string)
        {
            if (!NALSyntax.valid_term_chars.Contains(c)) return false;
        }

        return true;
    }

    private static ConcurrentDictionary<Type, IReadOnlyDictionary<ulong, string>> _cache;

    public static string stringValueOf<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if(_cache == null ) _cache = new ConcurrentDictionary<Type, IReadOnlyDictionary<ulong, string>>();
        var type = typeof(TEnum);
        var map = _cache.GetOrAdd(type, _ => BuildMap<TEnum>());
        var key = Convert.ToUInt64(value);
        return map.TryGetValue(key, out var s) ? s : value.ToString();
    }

    private static IReadOnlyDictionary<ulong, string> BuildMap<TEnum>() where TEnum : struct, Enum
    {
        var type = typeof(TEnum);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        var dict = new Dictionary<ulong, string>(fields.Length);
        foreach (var f in fields)
        {
            var key = Convert.ToUInt64(f.GetRawConstantValue());
            var desc = f.GetCustomAttribute<DescriptionAttribute>(false)?.Description;
            dict[key] = string.IsNullOrEmpty(desc) ? f.Name : desc;
        }
        return dict;
    }


    // given the "description" or string value, get the corresponding enum.
    public static TEnum? enumValueOf<TEnum>(string description) where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues(typeof(TEnum)).Cast<TEnum>())
        {
            if (stringValueOf(value).Equals(description))
                return value;
        }
        return null;
    }
}