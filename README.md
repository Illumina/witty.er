# witty.er

What is true, thank you, earnestly. A large variant evaluation tool upgraded from [wit.ty](https://git.illumina.com/DASTE/Ilmn.Das.App.Witty/blob/develop/Witty/README.md)

Developers: **[Yinan Wan](mailto:ywan@illumina.com)**, **[Kent Ho](mailto:kho@illumina.com)**

Release Notes: **[Witty.er release notes](docs/release-notes/README.md)**

---

## System Requirements

- witty.er is built using C# dotnet core 2.0 framework.  
- This repo can be cloned and compiled using MS Build or run directly with dotnet.  
- To run in MS Windows or linux, just call dotnet Wittyer.dll.  
- Alternatively, Docker can be used. See [Quick Start](#quick-start)

## Quick Start

To compare a query VCF's passing variants against a truth VCF, use the following commmand line:

```bash
# first clone the repo
git clone https://github.com/Illumina/witty.er.git

# compile the repo
cd witty.er/Wittyer
dotnet publish . -c Release

# run the dll
dotnet bin/Release/netcoreapp2.0/Wittyer.dll -i input.vcf -t truth.vcf -o outputdir

# Or if Docker is installed and running (example is for Mac or Linux)
# NOTE: Mac Docker has a default memory limit of 2GB, but the program needs more memory than that.
# Please see https://docs.docker.com/docker-for-mac/#advanced to set the docker container memories to a minimum of 4GB.
git clone https://github.com/Illumina/witty.er.git
docker build witty.er -t wittyer

docker run --rm \
  --workdir $(pwd) \
  -v $(pwd):$(pwd) \
  wittyer \
  -i input.vcf -t truth.vcf -o outputdir
```

## FAQ

* Compatibility notes:
  * Delly
    * Update to latest delly which has this bug fixed or Remove all the CN annotations
  * PBSV
    * It has illegal characters (non-VCF spec legal) in reference.  Things like `R`, `K`, `W` etc.
  * sniffles
    * Filter out all SVTYPEs except ["DEL", "INS", "DUP", "INV", "CNV"]
  * HGSVC truth set
    * CIEND and CIPOS that are `0,.` or `.,0` or what not crashes since the intention is unclear.  Please filter out entries with these values for these tags.


## Contents

- [Design Description](#design-description)
  - [Overview](#overview)
  - [Structural Variant Comparison](#structural-variant-comparison)
    - [Deletions, Duplications and Inversions](#deletions-duplications-and-inversions)
    - [Breakends](#breakends)
    - [Insertions](#insertions)
  - [Copy Number Variant Comparison](#copy-number-variant-comparison)
    - [Event level support](#event-level-support)
    - [Base level support](#base-level-support)
  - [Cross type matching between CNV and SV/Directional matching for CNV](#cross-type-matching-between-cnv-and-svdirectional-matching-for-cnv)
  - [Metrics](#metrics)
  - [Vcf annotation](#vcf-annotation)
  - [Include Bed File](#include-bed-file)
- [Usage](#usage)
  - [Examples](#examples)
  - [Parameters](#parameters)
    - [Required parameters](#required-parameters)
    - [Other Parameters](#other-parameters)
  - [Configuration File](#configuration-file)
  - [Outputs](#outputs)

---

## Design Description

### Overview

- Currently witty.er does SV comparison on a per-type basis, and only supports the following SV types:  
  - Deletions (DEL), Insertions (INS), Duplications (DUP), Inversions (INV), Breakends (BND, which are split into TranslocationBreakends and IntraChromosomeBreakend types), and Copy Number Variants (CNV, which are split into CopyNumberGain, CopyNumberLoss, and CopyNumberReference types)
  - Witty.er requires _SVTYPE_ to be specified in the vcf INFO field, except for Reference sites.
- Inserted segment is not used in comparison for Insertion
  - Insertion is considered as a breakend and evaluated the same way
- CNVs require the CN sample tag in order to be processed.
  - CNVs' reference copy number is based on the GT tag. e.g. if it's 0/1 (diploid), reference copy is assumed to be 2.
  - DUP and DELs that have CN sample tags are processed as CNVs
  - CNV entries without GT is assumed to be diploid.
- Complex type comparisons are currently not supported (e.g insertions in a deletion)
- Limited support for Multi-allelic entries due to vcf annotation is overly complicated
  - Multi-alts with different _SVTYPEs_ are annotated as _NotAssessed(N)_ and completely left out from stats
  - For multi-alt CNV gains, witty.er currently only using total _CN_ for evaluation without regard to CN number of each individual ALT.
- Only paired Breakends are supported (single or triple, etc. breakends are not supported)
- Ref sites and Ref call samples (samples with GT of 0/0, 0, or 0|0, etc) and Reference Copy Number entries are evaluated as a CopyNumberReference type
- By default Genotypes match is required in order to be considered as TP, but there is a simpleCounting mode.
- For witty.er to ignore genotype information, use _simpleCounting_ evaluationMode by specifying _-em_ _sc_
- By default witty.er only takes _PASS_ filter entries, for multi-sample vcf, if sample tag _FT_ exist, it has to be _PASS_ as well for the **query vcf only**.
  - Include/Exclude filters will be supported
- If you are only interested in performance for certain regions (like confidence regions), witty.er supports passing in a bed file per SVTYPE via command line or config file.

### Structural Variant Comparison

#### Deletions, Duplications and Inversions

- The evaluation criteria of Deletions, Duplications and Inversions are controlled by the following factors:
  - Percentage distance (PD) - The distance between both boundaries should respect a number that's proportional to total SV length.
    - This is a distance defined by percentage of the variant length (by default 25%, so a 5kb DEL the distance cutoff is 250bp)
  - Basepair distance boundary (BPD) - By default 500bp for all types except Insertions, which are set at 100bp
  - CIPOS/CIEND
- If there's any overlap in between the intervals defined by PD and BPD around **BOTH** the POS and END of the query and the target truth variant, only then is it considered TP.
- For the ultimate boundary distance, the cutoff is defined in two different ways based on SV length

  - When PD < BPD (so if we use default parameter, the transition point is 10kb)
    - The cutoff for boundary distance is strictly PD
  - When PD > BPD
    - The cutoff for boundary distance is the merged interval of CIPOS/CIEND and BPD
    - This is the same as how we evaluate breakend in the [wit.ty](https://git.illumina.com/DASTE/Ilmn.Das.App.Witty/blob/develop/Witty/README.md#detailed-implementation)
  - A typical example of what it meant by PD and BPD

    ![Pd and Bpd demo](docs/pd_and_bpd_demo.png)

- In general, the cutoff trend will look like this:

__note this is not using default parameters__

- Without CIPOS/CIEND:

![Pd and Bpd example1](docs/Pd_and_bpd_example1.png)

- Including CIPOS/CIEND:

  ![Pd and Bpd example2](docs/Pd_and_bpd_example2.png)

- Real examples:
  - A typical TP variant
    ![Deletion Tp example](docs/Deletion_tp_example.png)
  - A query that was considered as TP in wit.ty but is now considered as FP in witty.er:
    ![Deletion Fp example](docs/Deletion_fp_example.png)

#### Breakends

IntraChromosomeBreakends is evaluated just like DEL/DUP, but TranslocationBreakends are going to be evaluated by breakend distance. Note the comparison and stats are still within type (so Insertion always compare with only Insertion).

- **Breakends**  
  IntraChromosomeBreakends are using the same acceptance criteria as [DEL/DUP](#deletions-duplications-and-inversions), except that PD will be based on the distance between the two breakends:
  - In the following example, with 5% PD = (2001-1)\*5%=100

```
1       1 TRUTH_BND1   G       [1:2001[G   10    PASS        SVTYPE=BND
```

- For TranslocationBreakend (aka inter-chromosome breakend), PD is considered to be infinite, so BPD/CIPOS/CIEND is always used.
- Two BND entries (one breakend pair) is considered as TP of 1
- If genotype matching turned on, genotype of the interpreted breakend pair has to match
- IntraChromosomeBreakend stats will now be binned by the distance between two breakend in a pair.
- TranslocationBreakend will be in their own category with no bins.

#### Insertions

- **Insertions**  
  Insertions are evaluated only by distance to insertion site.

  - PD does not apply to Insertion (since they are inserted into 1 place spanning 0 reference bases)
  - Only BPD will be used since that will be the distance to insertion site.
  - Inserted segment sequence is not taken into consideration.
  - Insertion stats will be binned, though it'll be based on Insertion size/SVLEN.
    - Insertions of unknown size will always fall into the largest bin.

### Copy Number Variant Comparison

#### Event level support

- Event overlapping will use the same way as [DEL/DUP](#deletions-duplications-and-inversions) evaluation.
  - Evaluation criteria is percentage distance (25% by default) or basepair distance (500bp by default) depending on the SV size (see above explanations)
- Additional evaluation criteria includes
  - CN sample tag match (cross type would be directional match)
  - By default GT has to match (turn on simpleCounting mode _--em_ _sc_ to ignore GT match)

#### Base level support

- Base level stats does not take event-level true or false into consideration
- All bases overlapped and considered true (i.e. same CN/GT, etc) is considered TP
- All bases overlapped and considered false (i.e. different CN or GT, etc) is considered FP
- All bases that are not overlapped, but is present in truth is considered FN
- All bases that are not overlapped, but is present in query is considered FP
- All bases that are not in Truth or Query will **NOT** be assessed and not included in any stats
- Base level overlap for each entry will be reported in each vcf entry (WOW tag, see below), regardless if the event is True or False

### Cross type matching between CNV and SV/Directional matching for CNV

- Turned on my using "_-em_ _cts_" (EvaluationMode CrossTypeAndSimpleCounting)
- CopyNumberGain will be treated as DUP and CopyNumberLoss will be treated as DEL, evaluation process is thus the same as [DEL/DUP](#deletions-duplications-and-inversions) evaluation.
- The stats will be reported under "Deletion" and "Duplication" section.
- When _--crossMatching_ mode is turned on, CNV gain stats will be merged with other DUP stats and CNV loss stats will be merged with DEL stats.
- CopyNumberReference will still be evaluated as normal

**Note**:

- For cross type matching, we do not support the situation of multi-allellic CNV with at least one DEL and one DUP, due to the fact merging wittyer vcf annotation tags are over complicated.

### Metrics

- Stats will be reported in json format
- Reported as per sample pair, per variant type, per bin
  - There will be overall event and base level stats for the sample pair (across all variant types) and for each variant type (across all bins)
    - **IMPORTANT NOTE:** Because entries from different variant types and different bins can overlap positional coordinates, the base-level stat totals may not add up to the overall stats
      - OverallStats are the accurate stats to use for things like callability, deriving these stats by adding the numbers will result in overestimations.
  - Stats for Insertions and Breakends that are within the same chromosome (IntraChromosomeBreakends) **ARE** binned.
  - Stats for Breakends that connect two chromosomes (aka TranslocationBreakends) are not binned.
- Insertions, Inversions and IntraChromosomeBreakends will only have event level stats reported
- Deletion, Duplication, CopyNumberGain, CopyNumberLoss, and CopyNumberReference types will have both event and base level stats reported
- The results of certain bins can be ignored in the calculating and reporting of stats by prepending them with an `!` in the `--binSizes` argument. See `--binSizes` in [other parameters](#other-parameters).
- Example:

```json
{
          "VariantType": "CopyNumberGain",
          "OverallStats": [
            {
                "StatsType": "Event",
                "TruthTpCount": 0,
                "TruthFnCount": 89,
                "TruthTotalCount": 89,
                "Recall": 0.0,
                "QueryTpCount": 0,
                "QueryFpCount": 138,
                "QueryTotalCount": 138,
                "Precision": 0.0,
                "Fscore": "NaN"
            },
            {
                "StatsType": "Base",
                "TruthTpCount": 0,
                "TruthFnCount": 151822,
                "TruthTotalCount": 151822,
                "Recall": 0.0,
                "QueryTpCount": 0,
                "QueryFpCount": 113471,
                "QueryTotalCount": 113471,
                "Precision": 0.0,
                "Fscore": "NaN"
            }
          ],
          "PerBinStats": [
            {
              "Bin": "[1, 10000)",
              "BasicStats": [
                {
                  "StatsType": "Event",
                  "TruthTpCount": 0,
                  "TruthFnCount": 0,
                  "TruthTotalCount": 0,
                  "Recall": "NaN",
                  "QueryTpCount": 0,
                  "QueryFpCount": 0,
                  "QueryTotalCount": 0,
                  "Precision": "NaN",
                  "Fscore": "NaN"
                },
                {
                  "StatsType": "Base",
                  "TruthTpCount": 0,
                  "TruthFnCount": 0,
                  "TruthTotalCount": 0,
                  "Recall": "NaN",
                  "QueryTpCount": 0,
                  "QueryFpCount": 0,
                  "QueryTotalCount": 0,
                  "Precision": "NaN",
                  "Fscore": "NaN"
                }
              ]
            },
            {
              "Bin": "10000+",
              "BasicStats": [
                {
                  "StatsType": "Event",
                  "TruthTpCount": 0,
                  "TruthFnCount": 89,
                  "TruthTotalCount": 89,
                  "Recall": 0.0,
                  "QueryTpCount": 0,
                  "QueryFpCount": 138,
                  "QueryTotalCount": 138,
                  "Precision": 0.0,
                  "Fscore": "NaN"
                },
                {
                  "StatsType": "Base",
                  "TruthTpCount": 0,
                  "TruthFnCount": 151822,
                  "TruthTotalCount": 151822,
                  "Recall": 0.0,
                  "QueryTpCount": 0,
                  "QueryFpCount": 113471,
                  "QueryTotalCount": 113471,
                  "Precision": 0.0,
                  "Fscore": "NaN"
                }
              ]
            }
          ]
        }
```

### Vcf annotation

- Example vcf lines:

```
1       1 TRUTH1   N       <CN0>   4.56    PASS        SVTYPE=CNV;END=20000;CNVLEN=20000;CIPOS=0,400;CIEND=-800,1200     GT:CN:WHAT:WIT:WHY:WOW:WHO:WIN:WHERE      .:.:.:.:.:.:.:.:.  0/1:1:am:FP:Multiple:100,19800|19900,200000:QUERY1,QUERY2:CopyNumberLoss|10000-49999:50|300|500|700,19850|19200|1300|500

1       101 QUERY1   N       <CN0>   4.56    PASS        SVTYPE=CNV;END=19800;CNVLEN=19200;CIPOS=-50,600;CIEND=-100,700     GT:CN:WHAT:WIT:WHY:WOW:WHO:WIN:WHERE          1/1:0:am:FP:GTmismatch:101,19800:TRUTH1:CopyNumberLoss|10000-49999:50|300|500|700  .:.:.:.:.:.:.:.:.
1       19901 QUERY2   N       <CN0>   4.56    PASS        SVTYPE=CNV;END=21000;CNVLEN=1100     GT:CN:WHAT:WIT:WHY:WOW:WHO:WIN:WHERE          0/1:1:lm:FP:FailedBoundary:19901,20000:TRUTH1:CopyNumberLoss|1000-10000:19850|19200|1300|500   .:.:.:.:.:.:.:.:.
```

- Witty.er provides following tags for annotation purpose:
  - **WHO** A list of unique IDs representing the top ten matching events (could be less if there are less matches) in the same order as WHAT and WHERE.
    The ID is default to be the position of truth but if there are collisions, the ID will be the truth position incremented to the first unique number.
  - **WHAT** A top ten list of match types in the same order as WHO and WHERE, which could be either lm, am, lgm, or agm (local match, allele match, local genotype match, allele genotype match, respectively).
    - A local match means it has at least 1bp overlap with a target.
    - An allele match means it has overlap AND has matches the minimimum match criteria (BPD/PD)
    - A genotype match means that it also has a match in their Genotype (GT) sample fields.
  - **WIT** for witty.er decision based on the best match. Potential values are (TP/FP/FN/N). N means not assessed.
  - **WHY** as an extension of WIT to explain reasons for FP and N.
  - **WOW** witty.er overlap window of DEL/DUP/CNV/REF to support base level stats
  - **WHERE** A top ten list of border distances in the same order as WHAT and WHO, which consist of four numbers separated by pipes (|) describing the boundary distances between the entry and the match
  - **WIN** Additional match status information for stratification (e.g. Insertion|1-999 or Insertion|1000-9999 or TranslocationBreakend, which has no bins).
- Detailed vcf spec can be found [here](<https://confluence.illumina.com/pages/viewpage.action?pageId=201077378#Proposedwit.tyupgrade(akawitty.er)-Detailedvcfspec>)

### Include Bed File

Users can pass in a bed file (via the command line or on a per-SVTYPE basis via the config file) to have witty.er only assess TP/FP/FN decisions on entries that are completely contained within the included bed regions.
Note that even if an event is completely outside or only partially inside the bed region, it will still be matched against other entries and have the correct annotations assigned except for WIT will become NotAssessed
and the first WHY that would've been Missing Value (.) because it was supposed to be a TP will have OutsideBedRegion as the NotAssessed Reason. Therefore, it is possible that there is an entry that is completely in
the included bed region that is assigned TP/FP/FN, but its best match(es) are considered NotAssessed because the partner is not completely within the included bed region. CIPOS and CIEND are taken into consideration
when determining if an entry is included allowing CIPOS and CIEND to make the bed region more permissive. For Paired Breakends, both breakends must be within the included bed region to be assessed. Also note that
base-level stats will only count bases within the included bed regions as well.

Note that providing an include bed file can result in different values for QueryTP and TruthTP. This is because QueryTP is calculated on entries in the query that are completely inside the bed regions, while TruthTP is calculated on entries in the truth that are completely inside the bed regions.

## Usage

### Examples

- Turn on simple counting mode

```bash
## for multi-sample vcf, if either or both vcfs are multi-sample, sample pair information is required.  If -sp not present, it means to just compare the first columns
## to compare truthA with queryA and truthB with queryB
dotnet Wittyer.dll -i input.vcf -t truth.vcf -o outputDir -em sc -sp truthA:queryA,truthB:queryB

## to compare truthA with queryA, you could leave out truthA:queryA from the parameters
dotnet Wittyer.dll -i input.vcf -t truth.vcf -o outputDir -em sc
```

- Turn on cross-type matching mode (will be forced to be simpleCounting, see [here](#cross-type-matching-between-cnv-and-svdirectional-matching-for-cnv))

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf -o outputDir -em cts
```

- Only consider query entry to be TP when it is 100% overlap with truth entry.

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf --po 1.0 --bpd 0 -o outputDir
```

- Update the overlap interval for Breakend to be 100 to be consider as truth

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf --bpd 100
```

- Cacluate total stats for all entries (PASS and non-PASS filter entries) with no binning performed

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf --bs 0 --if
```

- Calculate stats for entries with L10kb filters

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf --if L10kb
```

- Calculate stats for entries with L10kb filters, but NOT with LowGQX filter

```bash
dotnet Wittyer.dll -i input.vcf -t truth.vcf --if L10kb -ef LowGQX
```

### Parameters

#### Required parameters

> **-i, --inputVcf=VALUE**  

        Query vcf file (only support one file for now)

> **-t, --truthVcf=VALUE**  

        Truth vcf file (currently only support one file)

#### Other Parameters

> **--bpd, --bpDistance=VALUE**  

        Upper bound of boundary distance when
        comparing truth and query.
        By default it is 500bp for all types
        except for Insertions, which are 100bp.
        Please note that if you set this value
        in the command line, it overrides all
        the defaults, so Insertions and other
        types will have the same bpd.  If
        you want customization, please use the
        -c config file option.

> **--pd, --percentDistance=VALUE**  

        In order to consider truth and query to be the
        same, the distance between both boundaries
        should be within a number that's proportional
        to total SV length.  Input this as a decimal,
        by default is 0.25.

> **--bs, --binSizes=VALUE**  

        Comma separated list of bin sizes. Default is
        1000, 10000 which means there are 3 bins: [1,
        1000), [1000,10000), [10000, >10000). 
        
        You can ignore certain bins in the calculation of
        performance statistics by prepending them with
        an '!'. For example, "!1,1000,5000,!10000"
        will ignore classifications in the [1, 1000)
        and [10000+) bins when calculating and reporting
        statistics. Calls will still be made in these bins
        in the Wittyer vcf though.

> **--if, --includedFilters=VALUE**  

        Comma separated list. Only variants contain these
        filters will be considered. By default is PASS.
        Use Empty String ("") to include all filters.

> **--ef, --excludedFilters=VALUE**  

        Comma separated list. Variants with any of these
        filters will be excluded in comparison. If any
        variants have filters conflicting with those in
        the included filters, excluded filters will take
        priority.

> **-o, --outputDirectory=VALUE**  

        Directory where all output files located

> **--sp, --samplePair=VALUE**  

        Optional unless either or both query and truth
        vcfs have more than one sample column.Comma
        separated list of truth to query sample
        mappings using colon (:) as the delimiter. For
        convenience, if you just want the first column
        compared, you can just provide this option with
        empty contents instead.For example, Truth1:
        Query1,NA12878:NA1278_S1

> **--em, --evaluationMode=VALUE**  

        Choose your evaluation mode, options are 'Default'('d')
        , 'SimpleCounting'('sc'), '
        CrossTypeAndSimpleCounting'('cts'), by default it'
        s using 'Default' mode, which does comparison
        by SvType and requires genotyping match

> **-vt, --variantTypes=VALUE**  

        Comma separated list of variant types to include in analysis.
        Acceptable variant types are CopyNumberGain, CopyNumberLoss,
        TranslocationBreakend, CopyNumberReference, Inversion,
        IntraChromosomeBreakend,  Insertion, Duplication, and Deletion.
        All variant types are included by default. **Case-sensitive**.

> **-c, --configFile=VALUE**  

        Config file used to specify per variant type settings. Used in place of
        bin sizes, basepair distance, percent distance, included filters,
        excluded filters, variant types and include bed arguments.
        See [Configuration File](#configuration-file).

> **-b, --includeBed=VALUE**  

        Bed file used to specify regions included in the analysis. Variants not
        completely within bed file regions will be marked as not assessed.
        This parameter is optional, and by default all variants will be analyzed.
        See [Include Bed File](#include-bed-file)
        Using command line parameter would use the same bed file for all SVTYPes.
        If you want a different one per SVTYPE, use the ConfigFile Option.
        See [Configuration File](#configuration-file).

> **-v, --version**  

        witty.er version information

> **-h, --help**  

        Show this message and exit

### Configuration File

Instead of global settings for basepair distance, percent distance, bin sizes, included filters, excluded filters, and include bed file, a json-format configuration file can be used to specify per variant type settings.

- Pass in a configuration file via the '-c,-\-configFile=' command line argument. - The '-c,-\-configFile=' argument cannot be used in combination with arguments for --bpDistance, --percentDistance, --binSizes, --includedFilters, --excludedFilters, --variantTypes, or --includeBed.
- Only the variant types listed in the configuration file will be analyzed.
- There are no default settings; for each variant type listed in the configuration file, all the relevant settings must be explicitly set.
- Json property names should be [camel case](https://en.wikipedia.org/wiki/Camel_case) and are case sensitive.
- The configuration used for analysis will be output in the result folder even if no config file was specified on the commandline, you can then rerun using that config file to ensure the same settings are used. - If a path to a nonexistent file is used for the config file, Wittyer will generate a default configuration file
  at the specified empty path for future use.
- Default configuration file, equivalent to running Wittyer without specifying settings for basepair distance, percent distance, bin sizes, included filters, excluded filters, or variant types:

```json
[
  {
    "variantType": "Duplication",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "Deletion",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "CopyNumberGain",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "CopyNumberLoss",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "CopyNumberReference",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "Inversion",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "IntraChromosomeBreakend",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 500,
    "percentDistance": 0.25,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "Insertion",
    "includeBed": "",
    "binSizes": "1000,10000",
    "bpDistance": 100,
    "includedFilters": "PASS",
    "excludedFilters": ""
  },
  {
    "variantType": "TranslocationBreakend",
    "includeBed": "",
    "bpDistance": 500,
    "includedFilters": "PASS",
    "excludedFilters": ""
  }
]
```

### Outputs

Currently witty.er provides:

- An overall per-sample-pair stats summary.
  An example of overall stats summary from stdout:

```
Overall Stats:
Overall Precision: 19.471 %
Overall Recall: 78.768 %
Overall Fscore: 31.223 %
--------------------------------
QuerySample     TruthSample     QueryTotal      QueryTp QueryFp Precision       TruthTotal      TruthTp TruthFn Recall Fscore

NA12877 CephFather-12877        10091   1992    8099    19.7%       2550    1997    553 78.3%  31.479%
NA12878 CephMother-12878        10422   2002    8420    19.2%       2532    2006    526 79.2%  30.907%
```

- A Wittyer.stats.json with detailed per-sample-pair, per-svtype, per-bin stats, example [here](#metrics)

- Additional vcf file for each sample pair, with updated query and truth entries merged into one file, the sample columns are organized as TRUTH then QUERY, detailed vcf spec [here](#vcf-annotation)
