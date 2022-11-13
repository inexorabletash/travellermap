=pod

        This script compares a fixed-format sector file with the standard tab-delimited Spinward Marches.
        
        REQUIREMENTS
        ------------
        1. The 1105 spinward marches sector needs to be in tab-delimited format under /M1105.
        2. Your spinward marches sector has to be in a fixed-field format.


        FIXED-FIELD FORMAT
        ------------------
        This is a standard Traveller sector format with a two-line header.
        The first header line contains standardized field names.
        The second header line "underlines" those fields with dashes, in order to indicate the field's length.

        STANDARD FIELD NAMES
        --------------------
        Sector, SS, Hex, Name, UWP, Bases, Remarks, Zone, PBG, Allegiance, Stars, {Ix}, (Ex), [Cx], Nobility, W, RU.

        You may also use:
            A for Allegiance 
            B for Bases
            N for Nobility
            Z for Zone
            Stellar for Stars

=cut

use strict;

my $file = shift || die "SYNOPSIS: $0 path-to-your-subsector\n";

my @outputLabels = qw/Hex Name UWP Remarks {Ix} (Ex) [Cx] Nobility Bases Zone PBG W Allegiance Stars/;

my %outputFixedFormat = (
    Sector  => 6,
    SS      => 2,
    Hex     => 4,
    Name    => 20,
    UWP     => 9,
    Bases   => 2,
    Remarks => 43,
    Zone    => 1,
    PBG     => 3,
    Allegiance => 4,
    Stars   => 14,
    '{Ix}'  => 6,
    '(Ex)'  => 7,
    '[Cx]'  => 6,
    Nobility => 6,
    W        => 2,
    RU       => 6,
);

#############################################################################
#
#
#                      load the 1105 standard data
#
#
#############################################################################
open my $m1105, '<', 'M1105/Spinward Marches.tab';
my $headerLine = <$m1105>;
chomp $headerLine;
my @FIELDS = split /\t/, $headerLine;
my %standardDB = ();

foreach my $line (<$m1105>)
{
    chomp $line;
    # we know exactly how 1105 data is laid out.  no guessing.  no modifications.
    my ($sec, $ss, $hex, $name, $uwp, $bases, $rem, $zone, $pbg, $alleg, $stars, $ix, $ex, $cx, $nobility, $w, $ru) = split /\t/, $line;
    $stars =~ s/\s+$//;
    chomp $ix, $ex, $cx;
    $standardDB{ $hex } = {
       Sector       => $sec,
       SS           => $ss,
       Hex          => $hex,
       Name         => $name,
       UWP          => $uwp,
       Bases        => $bases,
       Remarks      => formatRemarks($rem),
       Zone         => $zone,
       PBG          => $pbg,
       Allegiance   => $alleg,
       Stars        => $stars,
       '{Ix}'       => $ix,
       '(Ex)'       => $ex,
       '[Cx]'       => $cx,
       Nobility     => $nobility,
       W            => $w,
       RU           => $ru,
   };
   $standardDB{$hex}->{line} = $line;
   $standardDB{$hex}->{formatted} = dumpUwp( $standardDB{ $hex } );
}
close $m1105;

#############################################################################
#
#
#                       process the target sector 
#
#
#############################################################################
open my $targetSectorFile, '<', $file; # e.g. M1120/1120_Spin.sec
my $hdr = <$targetSectorFile>;
chomp $hdr;
my @hdr = split /\s+/, $hdr;
my $dashes = <$targetSectorFile>;
my @dashes = split /\s+/, $dashes;
my %candidateDB = ();

foreach my $line (<$targetSectorFile>)
{
   # ASSUME the hex number ALWAYS comes first.
   chomp $line;
   my ($hex) = $line =~ /^(....)/;

   # break up the line based on the header data.
   my $start = 0;
   for my $i (0..scalar @hdr-1)
   {
       my $hdrLabel = $hdr[$i];
       my $dash     = $dashes[$i];
       my $len      = length($dash);
       my $value    = substr( $line, $start, $len );
       $value = '' if $value eq '-';
       $value =~ s/\s+$//;
       $start       += $len + 1;
       $candidateDB{$hex}->{ $hdrLabel } = $value;
   }

   # Fix header.
   $candidateDB{$hex}->{Allegiance} = delete $candidateDB{$hex}->{A};
   $candidateDB{$hex}->{Bases}      = delete $candidateDB{$hex}->{B};
   $candidateDB{$hex}->{Nobility}   = delete $candidateDB{$hex}->{N};
   $candidateDB{$hex}->{Zone}       = delete $candidateDB{$hex}->{Z};
   $candidateDB{$hex}->{Stars}      = delete $candidateDB{$hex}->{Stellar};
   
   # Now calculate.
   $candidateDB{$hex}->{line}   = $line;
   $candidateDB{$hex}->{Sector} = $standardDB{$hex}->{Sector}; # use the standard!
   $candidateDB{$hex}->{SS}     = $standardDB{$hex}->{SS};     # use the standard!
   $candidateDB{$hex}->{UWP}    = compareStrings( $candidateDB{$hex}->{ UWP }, $standardDB{$hex}->{ UWP });
   $candidateDB{$hex}->{PBG}    = compareStrings( $candidateDB{$hex}->{ PBG }, $standardDB{$hex}->{ PBG });
   $candidateDB{$hex}->{Remarks} = formatRemarks( $candidateDB{$hex}->{ Remarks });
   $candidateDB{$hex}->{formatted} = dumpUwp( $candidateDB{ $hex } );
}
close targetSectorFile;

#############################################################################
#
#
#                    output the result as fixed-format.
#
#
#############################################################################
my $underline = '';
foreach my $field (@outputLabels)
{
   my $len = $outputFixedFormat{ $field };
   $underline .= '-' x $len;
   $underline .= ' ';

   #
   #  Convert to "fixed format" header name.
   #
   my $abbr = $field;
   $abbr = 'B' if $field eq 'Bases';
   $abbr = 'Z' if $field eq 'Zone';
   $abbr = 'A' if $field eq 'Allegiance';
   $abbr = 'N' if $field eq 'Nobility';

   printf "%-${len}s ", $abbr;
}
print "\n$underline\n";

foreach my $hex (sort keys %candidateDB)
{
   die "ERROR: cannot find candidate hex $hex\n" unless $candidateDB{ $hex };
   die "ERROR: cannot find standard hex $hex\n"  unless $standardDB{ $hex };

   my %candidate = %{ $candidateDB{ $hex } };
   my %standard = %{ $standardDB{ $hex } };

   printf "%4s %-20s ", 
        $standard{Hex}, 
        $standard{Name};

   foreach my $field (@outputLabels)
   {
       next if $field =~ /^Sector|SS|Hex|Name$/;
       my $len = $outputFixedFormat{ $field };
       printf "%-${len}s ", compareField( $standard{ $field }, $candidate{ $field });
   }
   print "\n";
}

#############################################################################
#
#
#                                  utils
#
#
#############################################################################
sub formatRemarks
{
    my $rem = shift;
    return $rem if $rem =~ /\w{3}/; # if there's a code that's more than 2 chars, forget it
    return join ' ', sort split /\s+/, $rem;
}

sub compareField
{
    my $standard  = shift;
    my $candidate = shift;

    return '*' if $candidate eq $standard;
    return $candidate;
}

sub compareStrings
{
    my ($changed, $standard) = @_;

    return '*' if $changed eq $standard;

    my @changed = split //, $changed;
    my @standard = split //, $standard;
    my $out = '';
    foreach my $letter (@changed)
    {
        my $compareWith = shift @standard;
        $out .= '.'     if ($letter eq $compareWith) && ($letter ne '-');
        $out .= $letter if ($letter ne $compareWith) || ($letter eq '-');
    }
    return $out;
}

sub dumpUwp
{
    my $w = shift;
    return sprintf "%4s %-20s %9s %-43s %6s %7s %6s %-6s %-2s %1s %3s %-2s %4s %s\n", 
        $w->{'hex'},
        $w->{ 'name' },  
        $w->{ 'uwp' },  
        $w->{ 'rem' },
        $w->{ 'ix'  },
        $w->{ 'ex' },
        $w->{ 'cx' },
        $w->{ 'nobility' },
        $w->{ 'bases' },
        $w->{ 'zone' },
        $w->{ 'pbg' },
        $w->{ 'w' },
        $w->{ 'alleg' },
        $w->{ 'stellar' };
}


