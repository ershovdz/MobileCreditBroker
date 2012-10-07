using System;
using System.Net;
using System.Net.Browser;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO.IsolatedStorage;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Net.NetworkInformation;
using System.Windows.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Text;


namespace Credits
{
    public class Constants
    {
        public const string m_cacheDir = "Cache";
        public const string m_resourceDir = "Resources/Data";
        public const string m_localSeparator = "\\";
        public const string m_globalSeparator = "/";
        public const int m_maxPageCount = 10;
        public static readonly string[] m_credits = { "credit", "autocredit", "mortgage", "cards", "business", "deposit" };
    };

    public class Bank
    {
    }

    public class DataWithRestrictions
    {
        string m_restrictions;

        public DataWithRestrictions(string restrictions)
        {
            m_restrictions = restrictions;
        }

        public string Restrictions
        {
            get { return m_restrictions; }
            set { m_restrictions = value; }
        }
    }

    public class PartialWithdrawal : DataWithRestrictions
    {
        string m_min_balance;

        public PartialWithdrawal(string restrictions, string minBalance) :
            base(restrictions)
        {
            m_min_balance = minBalance;
        }

        public string MinBalance
        {
            get { return m_min_balance; }
            set { m_min_balance = value; }
        }
    }

    public class Rate
    {
        string m_currency;
        double m_min_value;
        double m_max_value;
        string m_base_coefficient;
        double m_min_initial_instalment;
        double m_max_initial_instalment;
        int m_min_period;
        int m_max_period;
        int m_min_sum;
        int m_max_sum;
        double m_state_program_discount;

        public string Currency
        {
            get { return m_currency; }
            set { m_currency = value; }
        }

        public double MinValue
        {
            get { return m_min_value; }
            set { m_min_value = value; }
        }

        public double MaxValue
        {
            get { return m_max_value; }
            set { m_max_value = value; }
        }

        public string BaseCoefficient
        {
            get { return m_base_coefficient; }
            set { m_base_coefficient = value; }
        }

        public double MinInitialInstalment
        {
            get { return m_min_initial_instalment; }
            set { m_min_initial_instalment = value; }
        }

        public double MaxInitialInstalment
        {
            get { return m_max_initial_instalment; }
            set { m_max_initial_instalment = value; }
        }

        public int MinPeriod
        {
            get { return m_min_period; }
            set { m_min_period = value; }
        }

        public int MaxPeriod
        {
            get { return m_max_period; }
            set { m_max_period = value; }
        }

        public int MinSum
        {
            get { return m_min_sum; }
            set { m_min_sum = value; }
        }

        public int MaxSum
        {
            get { return m_max_sum; }
            set { m_max_sum = value; }
        }

        public double StateProgramDiscount
        {
            get { return m_state_program_discount; }
            set { m_state_program_discount = value; }
        }
    }

    public class Deposit
    {
        string m_name;
        string m_id;
        string m_link;
        string m_description;
        string m_bank;
        string m_restrictions;
        Rate[] m_rates;
        PartialWithdrawal m_partial_withdrawal;
        DataWithRestrictions m_replenishment;
        string m_rate_increase;
        string m_auto_prolongation;
        string m_pensionary;
        string m_multicurrency;
        string m_early_withdrawal;
        string m_online_for_existing;
        string m_remote_opening;
        string m_special_conditions;

        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        public string Id
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public string Link
        {
            get { return m_link; }
            set { m_link = value; }
        }

        public string Description
        {
            get { return m_description; }
            set { m_description = value; }
        }

        public string Bank
        {
            get { return m_bank; }
            set { m_bank = value; }
        }

        public string Restrictions
        {
            get { return m_restrictions; }
            set { m_restrictions = value; }
        }

        public DataWithRestrictions Replenishment
        {
            get { return m_replenishment; }
            set { m_replenishment = value; }
        }

        public PartialWithdrawal PartialWithdrawal
        {
            get { return m_partial_withdrawal; }
            set { m_partial_withdrawal = value; }
        }

        public string RateIncrease
        {
            get { return m_rate_increase; }
            set { m_rate_increase = value; }
        }

        public string AutoProlongation
        {
            get { return m_auto_prolongation; }
            set { m_auto_prolongation = value; }
        }

        public string Pensionary
        {
            get { return m_pensionary; }
            set { m_pensionary = value; }
        }

        public string Multicurrency
        {
            get { return m_multicurrency; }
            set { m_multicurrency = value; }
        }

        public string EarlyWithdrawal
        {
            get { return m_early_withdrawal; }
            set { m_early_withdrawal = value; }
        }

        public string OnlineForExisting
        {
            get { return m_online_for_existing; }
            set { m_online_for_existing = value; }
        }

        public string RemoteOpening
        {
            get { return m_remote_opening; }
            set { m_remote_opening = value; }
        }

        public string SpecialConditions
        {
            get { return m_special_conditions; }
            set { m_special_conditions = value; }
        }
    }

    public class Credit
    {
        string m_name;
        string m_id;
        string m_link;
        string m_description;
        string m_bank;
        string m_payment;
        string m_commision;
        string m_adv_repayment;
        string m_proof_income_documents;
        string m_other_provided_documents;
        string m_debtor_requirements;
        string m_credit_security;
        string m_decision_timing;
        string m_additional_info;
        string m_insurance;
        List<Rate> m_rates;

        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        public string Id
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public string Link
        {
            get { return m_link; }
            set { m_link = value; }
        }

        public string Description
        {
            get { return m_description; }
            set { m_description = value; }
        }

        public string Bank
        {
            get { return m_bank; }
            set { m_bank = value; }
        }

        public string Payment
        {
            get { return m_payment; }
            set { m_payment = value; }
        }

        public string Commision
        {
            get { return m_commision; }
            set { m_commision = value; }
        }

        public string AdvRepayment
        {
            get { return m_adv_repayment; }
            set { m_adv_repayment = value; }
        }

        public string ProofIncomeDocuments
        {
            get { return m_proof_income_documents; }
            set { m_proof_income_documents = value; }
        }

        public string OtherProvidedDocuments
        {
            get { return m_other_provided_documents; }
            set { m_other_provided_documents = value; }
        }

        public string DebtorRequirements
        {
            get { return m_debtor_requirements; }
            set { m_debtor_requirements = value; }
        }

        public string CreditSecurity
        {
            get { return m_credit_security; }
            set { m_credit_security = value; }
        }

        public string DecisionTiming
        {
            get { return m_decision_timing; }
            set { m_decision_timing = value; }
        }

        public string AdditionalInfo
        {
            get { return m_additional_info; }
            set { m_additional_info = value; }
        }

        public string Insurance
        {
            get { return m_insurance; }
            set { m_insurance = value; }
        }

        public List<Rate> Rates
        {
            get { return m_rates; }
            set { m_rates = value; }
        }

        public Rate Rate1
        {
            get { return m_rates.Count != 0 ? null : m_rates[0]; }
        }
 
 /*       public string Rate
        {
            get 
            { 
                return m_rates.Count == 0 ? null : "TestTestTest"; 
            }
        }
  */
    }


    public class PersonalCredit : Credit
    {
        string m_restrictions;
        string m_purpose;
        string m_locus_contractus;

        public string Restrictions
        {
            get { return m_restrictions; }
            set { m_restrictions = value; }
        }

        public string Purpose
        {
            get { return m_purpose; }
            set { m_purpose = value; }
        }

        public string LocusContractus
        {
            get { return m_locus_contractus; }
            set { m_locus_contractus = value; }
        }
    }

    public class AutoCredit : Credit
    {
        string m_seller;
        string m_vehicle_type;
        string m_vendor_type;
        string m_age_type;
        string m_age_mileage_restrictions;
        string m_state_program;
        string m_refinancing;

        public string Seller
        {
            get { return m_seller; }
            set { m_seller = value; }
        }

        public string VehicleType
        {
            get { return m_vehicle_type; }
            set { m_vehicle_type = value; }
        }

        public string VendorType
        {
            get { return m_vendor_type; }
            set { m_vendor_type = value; }
        }

        public string AgeType
        {
            get { return m_age_type; }
            set { m_age_type = value; }
        }

        public string AgeMileageRestrictions
        {
            get { return m_age_mileage_restrictions; }
            set { m_age_mileage_restrictions = value; }
        }

        public string StateProgram
        {
            get { return m_state_program; }
            set { m_state_program = value; }
        }

        public string Refinancing
        {
            get { return m_refinancing; }
            set { m_refinancing = value; }
        }
    }

    public class Mortgage : Credit
    {
        string m_changing_rate;
        string[] m_dwellings;
        string m_dwelling_readiness;
        string m_refinancing;

        public string ChangingRate
        {
            get { return m_changing_rate; }
            set { m_changing_rate = value; }
        }

        public string[] Dwellings
        {
            get { return m_dwellings; }
            set { m_dwellings = value; }
        }

        public string DwellingReadiness
        {
            get { return m_dwelling_readiness; }
            set { m_dwelling_readiness = value; }
        }

        public string Refinancing
        {
            get { return m_refinancing; }
            set { m_refinancing = value; }
        }
    }
}
