# CNV witty.er Examples

In this folder, you will find a dragen CNV output file ([HG002.cnv.vcf.gz](HG002.cnv.vcf.gz)) and a [config.json](config.json), which will be used as input.  Example output can be found in the [output folder](output).

1. Download the NIST truth set:
   1. VCF file: ftp://ftp-trace.ncbi.nlm.nih.gov/giab/ftp/data/AshkenazimTrio/analysis/NIST_SVs_Integration_v0.6/HG002_SVs_Tier1_v0.6.vcf.gz
   2. BED file: ftp://ftp-trace.ncbi.nlm.nih.gov/giab/ftp/data/AshkenazimTrio/analysis/NIST_SVs_Integration_v0.6/HG002_SVs_Tier1_v0.6.bed
2. Install `dotnet` if you don't already have it installed on your system (you can google directions on how to install based on your system).
3. Build `witty.er` with the `dotnet publish` command under the `Wittyer` project folder
4. Run the following command line:

```bash
# DOTNET is your path to your dotnet executable
# WITTYER_DLL is the path to the wittyer dll.
$DOTNET $WITTYER_DLL \
         -t NIST_v0.6/HG002_SVs_Tier1_v0.6.vcf.gz \
         -i HG002.cnv.vcf.gz \
         -em CrossTypeAndSimpleCounting \
         --configFile config.json \
         --includeBed NIST_v0.6/HG002_SVs_Tier1_v0.6.bed \
         -o output/
```