/*
    Author: Christian Hahm
    Created: May 12, 2022
    Purpose: Enforces Narsese grammar that == used throughout the project
*/

/*
Helper Functions
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

public static class TermHelperFunctions
{
    public static bool is_valid_statement(Term term)
    {
        return term is StatementTerm || (term is CompoundTerm && !((CompoundTerm)term).is_first_order());
    }


    public static ConcurrentDictionary<string, Term> created_terms = new();

    public static CompoundTerm TryGetCompoundTerm(List<Term> sub_terms, TermConnector term_connector, List<int>? interval_list = null)
    {
        var subterms = sub_terms;
        var connector = term_connector;
        List<int> intervals = null;
        bool is_an_extensional_set = (term_connector == TermConnector.ExtensionalSetStart);
        bool is_an_intensional_set = (term_connector == TermConnector.IntensionalSetStart);
        bool is_a_set = is_an_extensional_set || is_an_intensional_set;
    
        bool is_operation = false;
        if (sub_terms.Count > 1)
        {
            // handle interval_list for the relevant temporal connectors.
            if (term_connector == TermConnector.SequentialConjunction)
            {
                // (A &/ B ...)
                if (interval_list != null && interval_list.Count > 0)
                {
                    intervals = interval_list;
                }
                else
                {
                    // if generic conjunction from input, assume interval of 1
                    // todo accept interval_list from input
                    intervals = Enumerable.Repeat(1, sub_terms.Count - 1).ToList();
                }

                // this.string_with_interval = this._create_term_string_with_interval()
            }
            else if (term_connector == TermConnector.ParallelConjunction)
            {
                // (A &| B ...)
                // interval of 0
                intervals = Enumerable.Repeat(0, sub_terms.Count - 1).ToList();
            }

            // decide if we need to maintain the ordering
            if (TermConnectorMethods.is_order_invariant((TermConnector)term_connector))
            {
                // order doesn't matter, alphabetize so the system can recognize the same term
                sub_terms.Sort((x, y) => x.ToString().CompareTo(y.ToString()));
            }

            // check if it's a set

            // handle multi-component sets
            if (is_a_set)
            {
                //// todo handle multi-component sets better
                //List<Term> singleton_set_subterms = new List<Term>();

                //foreach (Term subterm in sub_terms)
                //{
                //    // decompose the set into an intersection of singleton sets
                //    CompoundTerm singleton_set_subterm = TermHelperFunctions.TryGetCompoundTerm(new List<Term> { subterm }, TermConnectorMethods.get_set_end_connector_from_set_start_connector((TermConnector)term_connector));

                //    singleton_set_subterms.Add(singleton_set_subterm);
                //}


                //subterms = singleton_set_subterms;

                // set new term connector as intersection
                if (is_a_set)
                {
                    connector = TermConnector.IntensionalIntersection;
                }
                else if (is_an_intensional_set)
                {
                    connector = TermConnector.ExtensionalIntersection;
                }

            }

            // store if this == an operation (meaning all of its components are)
            is_operation = true;
            for (int i = 0; i < subterms.Count; i++)
            {
                Term subterm = sub_terms[i];
                is_operation = is_operation && subterm.is_op();
            }
        }
        string term_string = CompoundTerm._create_term_string(is_a_set,connector,subterms);

        if (created_terms.ContainsKey(term_string)) return (CompoundTerm)created_terms[term_string];

        var compound = new CompoundTerm(subterms, connector, term_string, is_operation, intervals);
        return compound;
    }
    public static Term from_string(string term_string)
    {
        /*
            Determine if it is an atomic term (e.g. "A") || a statement/compound term (e.g. (&&,A,B,..) || (A --> B))
            || variable term && creates the corresponding Term.

            :param term_string - String from which to construct the term
            :returns Term constructed using the string
        */
        term_string = term_string.Replace(" ", "");
        //Asserts.assert(term_string.Length > 0, "ERROR: Cannot convert empty string to a Term.");

        if(created_terms.ContainsKey(term_string)) return created_terms[term_string];

       // if(created_terms.ContainsKey(term_string)) return created_terms[term_string];

        string statementStartString = SyntaxUtils.stringValueOf(StatementSyntax.Start);
        string statementEndString = SyntaxUtils.stringValueOf(StatementSyntax.End);
        Term term;
        if (Char.ToString(term_string[0]) == statementStartString)
        {
            /*
                Compound or Statement Term
            */
            //Asserts.assert(Char.ToString(term_string[term_string.Length - 1]) == statementEndString, "Compound/Statement term must have ending parenthesis: " + term_string);

            (Copula? copula, int copula_idx) = CopulaMethods.get_top_level_copula(term_string);
            if (copula == null)
            {
                // compound term
                term = CompoundTerm.from_string(term_string);
            }
            else
            {
                term = StatementTerm.from_string(term_string);
            }
        }
        else if (TermConnectorMethods.is_set_bracket_start(Char.ToString(term_string[0])))
        {
            // set term
            term = CompoundTerm.from_string(term_string);
        }
        else if (Char.ToString(term_string[0]) == VariableTerm.VARIABLE_SYM || Char.ToString(term_string[0]) == VariableTerm.QUERY_SYM)
        {
            // variable term
            int dependency_list_start_idx = term_string.IndexOf("(");
            string variable_name;
            string dependency_list_string;
            if (dependency_list_start_idx == -1)
            {
                variable_name = term_string[1..];
                dependency_list_string = "";
            }
            else
            {
                variable_name = term_string[1..dependency_list_start_idx];
                dependency_list_string = term_string[(term_string.IndexOf("(") + 1)..term_string.IndexOf(")")];
            }

            term = VariableTerm.from_string(variable_name,
                                            Char.ToString(term_string[0]),
                                            dependency_list_string);
        }
        else
        {
            term_string = Regex.Replace(term_string, @",\d+", "");
            term = new AtomicTerm(term_string);
        }

        created_terms.TryAdd(term_string,term);

        return term;
    }


    public static Term simplify(Term term)
    {
        /*
            Simplifies a term && its sub_terms,
            using NAL Theorems.

            :returns The simplified term
        */
        return term; // todo
        /*    simplified_term = term

            if isinstance(term, StatementTerm){
                    simplified_term = StatementTerm(subject_term = simplify(term.get_subject_term()),
                                                    predicate_term = simplify(term.get_predicate_term()),
                                                    copula = term.get_copula(),
                                                    interval = term.interval)
            } else if(isinstance(term, CompoundTerm)){
                        if term.connector == NALSyntax.TermConnector.Negation && \
                        len(term.sub_terms) == 1 && \
                        isinstance(term.sub_terms[0], CompoundTerm) && \
                        term.sub_terms[0].connector == NALSyntax.TermConnector.Negation:
                    // (--,(--,(S --> P)) <====> (S --> P)
                    // Double negation theorem. 2 Negations cancel out
                    simplified_term = simplify(term.sub_terms[0].sub_terms[0])  // get the inner statement
                // else if TermConnectorMethods.is_conjunction(term.connector){
                //         #(&&,A,B..C)
                //         new_subterms = []
                //         new_intervals = []
                //         for i in range(len(term.sub_terms)){
                //             subterm = simplify(term.sub_terms[i])
                //             if i < len(term.interval_list){ new_intervals.append(term.interval_list[i])
                //             if isinstance(subterm, CompoundTerm) && subterm.connector == term.connector:
                //                 // inner conjunction
                //                 new_subterms.extend(subterm.sub_terms)
                //                 new_intervals.extend(subterm.interval_list)
                //             else:
                //                 new_subterms.append(subterm)
        #
                    //         simplified_term = CompoundTerm(sub_terms=new_subterms,
                    //                             term_connector=term.connector,
                    //                                        interval_list=new_intervals)
                    else if term.connector == NALSyntax.TermConnector.ExtensionalDifference:
                    pass
                else if term.connector == NALSyntax.TermConnector.IntensionalDifference:
                    pass
                else if term.connector == NALSyntax.TermConnector.ExtensionalImage:
                    pass
                else if term.connector == NALSyntax.TermConnector.IntensionalImage:
                    pass

            return simplified_term;
                }
        */
    }
}



public abstract class Term
{
    /*
        Base class for all terms.
    */

    public string term_string;
    public int? syntactic_complexity;
    public TermConnector? connector = null;

    public Term()
    {
        this.term_string = "";
        this.syntactic_complexity = 0; // this._calculate_syntactic_complexity();
    }



    public override bool Equals(object other)
    {
        /*
            Terms are equal if their strings are the same
        */
        return other is Term && this.ToString() == other.ToString();
    }

    public static bool operator ==(Term obj1, Term obj2)
    {
        /*
            Terms are equal if their strings are the same
        */
        if (object.ReferenceEquals(obj1, obj2)) return true;
        if (object.ReferenceEquals(obj1, null) || object.ReferenceEquals(obj2, null)) return false;
        return obj1.ToString() == obj2.ToString();
    }

    public static bool operator !=(Term obj1, Term obj2)
    {
        /*
            Terms are equal if their strings are the same
        */
        return !(obj1 == obj2);
    }

    public override int GetHashCode()
    {
        return this.ToString().GetHashCode();
    }

    public override string ToString()
    {
        return this.term_string;
    }

    public virtual int _calculate_syntactic_complexity()
    {
        //Asserts.assert(false, "Complexity not defined for Term base class");
        return -1;
    }

    public virtual bool is_op()
    {
        return false;
    }

    public virtual bool contains_op()
    {
        return false;
    }

    public bool contains_variable()
    {
        return this.ToString().Contains(VariableTerm.VARIABLE_SYM) ||
               this.ToString().Contains(VariableTerm.QUERY_SYM);
    }

    public virtual string get_term_string()
    {
        return this.term_string;
    }
    
    public static Term from_string(string term_string)
    {
        return TermHelperFunctions.from_string(term_string);
    }

    public abstract bool is_first_order();

}

public class VariableTerm : Term
{
    public enum VariableType
    {
        Independent = 1,
        Dependent = 2,
        Query = 3,
    }

    public const string VARIABLE_SYM = "#";
    public const string QUERY_SYM = "?";

    public Term?[] dependency_list;
    public string variable_name;
    public VariableType variable_type;
    public string variable_symbol;


    public VariableTerm(string variable_name,
             VariableType variable_type,
             Term?[] dependency_list = null) : base()
    {
        /*

        :param variable_string: variable name
        :param variable_type: type of variable
        :param dependency_list: array of independent variables this vac# call riable depends on
        */
        // todo parse variable terms from input strings
        this.variable_name = variable_name;
        this.variable_type = variable_type;
        if (variable_type == VariableType.Query)
        {
            this.variable_symbol = VariableTerm.QUERY_SYM;
        }
        else
        {
            this.variable_symbol = VariableTerm.VARIABLE_SYM;
        }

        this.dependency_list = dependency_list;
        this._create_term_string();
    }


    public string _create_term_string()
    {
        string dependency_string = "";
        if (this.dependency_list != null)
        {
            dependency_string = "(";
            foreach (Term dependency in this.dependency_list)
            {
                dependency_string = dependency_string + dependency.ToString() + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
            }


            dependency_string = dependency_string[0..^1] + ")";
        }
        this.term_string = this.variable_symbol + this.variable_name + dependency_string;
        return this.term_string;
    }


    public static VariableTerm from_string(string variable_name, string variable_type_symbol, string dependency_list_string)
    {
        // parse dependency list
        List<Term>? dependency_list = null;

        if (dependency_list_string.Length > 0)
        {
            dependency_list = new List<Term>();
        }


        VariableTerm.VariableType? type = null;
        if (variable_type_symbol == VariableTerm.QUERY_SYM)
        {
            type = VariableTerm.VariableType.Query;
        }
        else if (variable_type_symbol == VariableTerm.VARIABLE_SYM)
        {
            if (dependency_list == null)
            {
                type = VariableTerm.VariableType.Independent;
            }
            else
            {
                type = VariableTerm.VariableType.Dependent;
            }
        }
        else
        {
            //Asserts.assert(false, "Error: Variable type symbol invalid");
        }

        return new VariableTerm(variable_name, (VariableTerm.VariableType)type, dependency_list.ToArray());
    }

    public override int _calculate_syntactic_complexity()
    {
        if (this.syntactic_complexity != null) return (int)this.syntactic_complexity;
        if (this.dependency_list == null)
        {
            return 1;
        }
        else
        {
            return 1 + this.dependency_list.Length;
        }
    }

    public override bool is_first_order()
    {
        return true;
    }
}


public class AtomicTerm : Term
{
    /*
        An atomic term, named by a valid word.
    */

    public AtomicTerm(string term_string) : base()
    {
        /*
        Input:
            term_string = name of the term
        */
        //Asserts.assert(SyntaxUtils.is_valid_term(term_string), term_string + " is not a valid Atomic Term name.");
        this.term_string = term_string;
    }

    public override bool is_first_order()
    {
        return true;
    }

    public override int _calculate_syntactic_complexity()
    {
        return 1;
    }


}


public class CompoundTerm : Term
{
    /*
        A term that contains multiple atomic sub_terms connected by a connector.

        (Connector T1, T2, ..., Tn)
    */

    public List<Term> subterms;
    public List<int> intervals;
    public bool is_operation;

    public CompoundTerm(List<Term> sub,
        TermConnector term_connector,
        string term_str,
        bool is_op,
        List<int>? intervals = null) : base()
    {
        /*
        Input:
            sub_terms: array of immediate sub_terms

            term_connector: subterm connector (can be first-order || higher-order).
                            sets are represented with the opening bracket as the connector, { || [

            interval_list: array of time interval_list between statements (only used for sequential conjunction)
        */
        if (TermConnectorMethods.is_order_invariant((TermConnector)term_connector))
        {
            // order doesn't matter, alphabetize so the system can recognize the same term
            sub.Sort((x, y) => x.ToString().CompareTo(y.ToString()));
        }
        if(sub.Count != sub.Distinct().Count())
        {
            UnityEngine.Debug.LogError("duplicate terms "+ subterms);
        }
        this.subterms = sub;
        this.connector = term_connector;
        this.intervals = intervals;
        this.is_operation = is_op;
        this.term_string = term_str;
    }

    public override bool is_op()
    {
        return this.is_operation;
    }

    public override bool contains_op()
    {
        foreach (Term subterm in this.subterms)
        {
            if (subterm.is_op()) return true;
        }

        return false;
    }
    public override bool is_first_order()
    {
        return TermConnectorMethods.is_first_order((TermConnector)this.connector);
    }

    public bool is_intensional_set()
    {
        return this.connector == TermConnector.IntensionalSetStart;
    }

    public bool is_extensional_set()
    {
        return this.connector == TermConnector.ExtensionalSetStart;
    }

    public bool is_set()
    {
        return this.is_intensional_set() || this.is_extensional_set();
    }

    public string? get_term_string_with_interval()
    {
        return null; //this.string_with_interval;
    }

    public string _create_term_string_with_interval()
    {
        string str;
        if (this.is_set())
        {
            str = SyntaxUtils.stringValueOf(this.connector.Value);
        }
        else
        {
            str = SyntaxUtils.stringValueOf(this.connector.Value) + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
        }

        for (int i = 0; i < this.subterms.Count; i++)
        {
            Term subterm = this.subterms[i];
            str += subterm.get_term_string() + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
            if (this.connector == TermConnector.SequentialConjunction && i < this.intervals.Count)
            {
                str += this.intervals[i].ToString() + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
            }
        }

        str = str[0..^1];  // remove the final term divider

        if (this.is_set())
        {
            return str + SyntaxUtils.stringValueOf(TermConnectorMethods.get_set_end_connector_from_set_start_connector((TermConnector)this.connector));
        }
        else
        {
            return SyntaxUtils.stringValueOf(StatementSyntax.Start) + str + SyntaxUtils.stringValueOf(StatementSyntax.End);
        }
    }


    public static string _create_term_string(bool is_set,
        TermConnector connector,
        List<Term> subterms)
    {
        string str;
        if (is_set)
        {
            str = SyntaxUtils.stringValueOf(connector);
        }
        else
        {
            str = SyntaxUtils.stringValueOf(connector) + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
        }

        for (int i = 0; i < subterms.Count; i++)
        {
            Term subterm = subterms[i];
            var term_string = subterm.get_term_string();
            str += term_string + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider);
        }

        str = str[0..^1];  // remove the final term divider

        if (is_set)
        {
            str += SyntaxUtils.stringValueOf(TermConnectorMethods.get_set_end_connector_from_set_start_connector((TermConnector)connector));
        }
        else
        {
            str = SyntaxUtils.stringValueOf(StatementSyntax.Start) + str + SyntaxUtils.stringValueOf(StatementSyntax.End);
        }

        return str;
    }



    public int _calculate_syntactic_complexity()
    {
        /*
            Recursively calculate the syntactic complexity of
            the compound term. The connector adds 1 complexity,
            && the sub_terms syntactic complexities are summed as well.
        */
        if (this.syntactic_complexity != null) return (int)this.syntactic_complexity;
        int count = 0;
        if (this.connector != null) count = 1;  // the term connector
        for (int i = 0; i < this.subterms.Count; i++)
        {
            Term subterm = subterms[i];
            count += subterm._calculate_syntactic_complexity();
        }

        return count;
    }


    public new static CompoundTerm from_string(string compound_term_string)
    {
        /*
            Create a compound term from a string representing a compound term
        */
        compound_term_string = compound_term_string.Replace(" ", "");
        (List<Term> subterms, TermConnector connector, List<int>? intervals) = CompoundTerm.parse_toplevel_subterms_and_connector(compound_term_string);
        return TermHelperFunctions.TryGetCompoundTerm(subterms, connector, intervals);
    }

    public static (List<Term>, TermConnector, List<int>?) parse_toplevel_subterms_and_connector(string compound_term_string)
    {
        /*
            Parse out all top-level sub_terms from a string representing a compound term

            compound_term_string - a string representing a compound term
        */
        compound_term_string = compound_term_string.Replace(" ", "");
        List<Term> subterms = new List<Term>();
        List<int> intervals = new List<int>();
        string internal_string = compound_term_string[1..^1];  // string with no outer parentheses () || set brackets [], {}

        // check the first char for intensional/extensional set [a,b], {a,b}
        // also check for array @
        TermConnector? connector = (TermConnector?)SyntaxUtils.enumValueOf<TermConnector>(Char.ToString(compound_term_string[0]));
        string connector_string;
        if (connector == null)
        {
            // otherwise check the first 2 chars for regular Term/Statement connectors
            if (Char.ToString(internal_string[1]) == SyntaxUtils.stringValueOf(StatementSyntax.TermDivider))
            {
                connector_string = Char.ToString(internal_string[0]);  // Term connector
            }
            else
            {
                connector_string = internal_string[0..2];  // Statement connector
            }
            connector = (TermConnector?)SyntaxUtils.enumValueOf<TermConnector>(connector_string);
            //Asserts.assert(internal_string[connector_string.Length].ToString() == SyntaxUtils.stringValueOf(StatementSyntax.TermDivider), "Connector not followed by comma in CompoundTerm string " + compound_term_string);
            internal_string = internal_string[(connector_string.Length + 1)..];
        }

        //Asserts.assert(connector != null, "Connector could not be parsed from CompoundTerm string.");

        int depth = 0;
        string subterm_string = "";
        for (int i = 0; i < internal_string.Length; i++)
        {
            String c = Char.ToString(internal_string[i]);
            if (c == SyntaxUtils.stringValueOf(StatementSyntax.Start) || TermConnectorMethods.is_set_bracket_start(c))
            {
                depth += 1;
            }
            else if (c == SyntaxUtils.stringValueOf(StatementSyntax.End) || TermConnectorMethods.is_set_bracket_end(c))
            {
                depth -= 1;
            }

            if (c == SyntaxUtils.stringValueOf(StatementSyntax.TermDivider) && depth == 0)
            {
                if (int.TryParse(subterm_string, out int subterm_string_as_int))
                {
                    intervals.Add(subterm_string_as_int);
                }
                else
                {
                    subterms.Add(Term.from_string(subterm_string));
                }
                subterm_string = "";
            }
            else
            {
                subterm_string += c;
            }
        }

        subterms.Add(Term.from_string(subterm_string));

        return (subterms, (TermConnector)connector, intervals);
    }

    public Term get_negated_term()
    {
        if (this.connector == TermConnector.Negation && this.subterms.Count == 1)
        {
            return this.subterms[0];
        }
        else
        {
            return TermHelperFunctions.TryGetCompoundTerm(new List<Term> { this }, TermConnector.Negation);
        }
    }
}
        
public class StatementTerm : Term
{
    /*
        <subject><copula><predicate>

        A special kind of compound term with a subject, predicate, && copula.

        (P --> Q)
    */
    public List<Term> subterms;
    public Copula copula;
    public int interval;
    public string string_with_interval = "";
    public bool is_operation;


    public StatementTerm(Term subject_term,
                 Term predicate_term,
                 Copula copula,
                 int interval = 0) : base()
    {
        /*
        :param subject_term:
        :param predicate_term:
        :param copula:
        :param interval: If first-order (an event){
                            the number of working cycles, i.e. the interval, before the event, if this event was derived from a compound
                        If higher-order (predictive implication)
                            the number of working cycles, i.e. the interval, between the subject && predicate events
        */
        //Asserts.assert_term(subject_term);
        //Asserts.assert_term(predicate_term);

        this.subterms = new List<Term> { subject_term, predicate_term };
        this.interval = interval;
        this.copula = copula;

        if (CopulaMethods.is_symmetric(copula))
        {
            this.subterms.Sort((x, y) => x.ToString().CompareTo(y.ToString())); //sort alphabetically
        }

        this.is_operation = this.calculate_is_operation();

        string term_str = this._create_term_string();
        this.term_string = term_str;
    }

    public override string ToString()
    {
        return this.term_string;
    }


    public new static StatementTerm from_string(string statement_string)
    {
        /*
            Parameter: statement_string - String of NAL syntax "(term copula term)"

            Returns: top-level subject term, predicate term, copula, copula index
        */
        statement_string = statement_string.Replace(" ", "");
        // get copula

        (Copula? copula, int copula_idx) = CopulaMethods.get_top_level_copula(statement_string);
        //Asserts.assert(copula != null, "Copula not found but need a copula. Exiting..");
        string copula_string = SyntaxUtils.stringValueOf(copula.Value);

        string subject_str = statement_string[1..copula_idx];  // get subject string
        string predicate_str = statement_string[(copula_idx + copula_string.Length)..^1];  // get predicate string

        int interval = 0;
        //if (!CopulaMethods.is_first_order((Copula)copula))
        //{
        //    string last_element = subject_str.Split(",")[^1];
        //    if (int.TryParse(last_element[0..^1], out _)) int.TryParse(last_element[0..^1], out interval);
        //}

        var subject_term = Term.from_string(subject_str);
        var predicate_term = Term.from_string(predicate_str);
        StatementTerm statement_term = new StatementTerm(subject_term, predicate_term, (Copula)copula, interval);

        return statement_term;
    }

    public new int _calculate_syntactic_complexity()
    {
        /*
            Recursively calculate the syntactic complexity of
            the compound term. The connector adds 1 complexity,
            && the sub_terms syntactic complexities are summed as well.
        */
        if (this.syntactic_complexity != null) return (int)this.syntactic_complexity;
        int count = 1;  // the copula
        foreach (Term subterm in this.subterms)
        {
            count += subterm._calculate_syntactic_complexity();
        }

        return count;
    }

    public Term get_subject_term()
    {
        return this.subterms[0];
    }

    public Term get_predicate_term()
    {
        return this.subterms[1];
    }

    public Copula get_copula()
    {
        return this.copula;
    }

    public string get_copula_string()
    {
        return SyntaxUtils.stringValueOf(this.get_copula());
    }

    public string get_term_string_with_interval()
    {
        return this.string_with_interval;
    }

    public string _create_term_string_with_interval()
    {
        /*
            Returns the term's string with interval_list.

            returns: (Subject copula Predicate)
        */
        string str;
        if (this.get_subject_term() is CompoundTerm && (((CompoundTerm)this.get_subject_term()).connector == TermConnector.SequentialConjunction))
        {
            str = SyntaxUtils.stringValueOf(StatementSyntax.Start) + ((CompoundTerm)this.get_subject_term()).get_term_string_with_interval();
        }
        else
        {
            str = SyntaxUtils.stringValueOf(StatementSyntax.Start) + this.get_subject_term().get_term_string();
        }

        if (!this.is_first_order() && this.interval > 0)
        {
            str = str[0..^1] + SyntaxUtils.stringValueOf(StatementSyntax.TermDivider) + this.interval.ToString() + str[0..^1];
        }

        str += " " + this.get_copula_string() + " ";
        str += this.get_predicate_term().get_term_string() + SyntaxUtils.stringValueOf(StatementSyntax.End);

        return str;
    }

    public string _create_term_string()
    {
        /*
            Returns the term's string.

            This is very important, because terms are compared for equality using this string.

            returns: (Subject copula Predicate)
        */
        string str = SyntaxUtils.stringValueOf(StatementSyntax.Start) + this.get_subject_term().get_term_string();

        str += " " + this.get_copula_string() + " ";
        str += this.get_predicate_term().get_term_string() + SyntaxUtils.stringValueOf(StatementSyntax.End);

        this.term_string = str;
        return this.term_string;
    }

    public override bool contains_op()
    {
        bool contains = this.is_op();
        if (!this.is_first_order())
        {
            contains = contains || this.get_subject_term().contains_op() || this.get_predicate_term().contains_op();
        }
        return contains;
    }

    public override bool is_op()
    {
        return this.is_operation;
    }

    public bool calculate_is_operation()
    {
        return this.get_subject_term() is CompoundTerm &&
        ((CompoundTerm)this.get_subject_term()).connector == TermConnector.Product &&
        ((CompoundTerm)this.get_subject_term()).subterms[0].ToString() == "{SELF}"; //todo reference to self_term.to_string here
    }

    public override bool is_first_order()
    {
        return CopulaMethods.is_first_order(this.copula);
    }

    public bool is_symmetric()
    {
        return CopulaMethods.is_symmetric(this.copula);
    }


    public Term get_negated_term()
    {
        return TermHelperFunctions.TryGetCompoundTerm(new List<Term> { this }, TermConnector.Negation);
    }

}

