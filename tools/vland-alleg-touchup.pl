=pod

    This script updates the allegiance codes for M1120 Vland subsectors M,N,O and P
    to more closely match the maps in: 
        (a) Rebellion Sourcebook for 1120 and 
        (b) Survival Margin for 1119.

    For subsectors M, N, O and P:
        (a) Preserve systems that don't have "StIm" or "Zisi".
        (b) Default remaining systems to 'NaFr' -- non-aligned frontier.
        (c) Set a tiny corner of systems in M to StIm.
        (d) Set a pile of designated systems to ZiSi.
    
    That's it.

=cut

use strict;

my %allegs;
my %allegCountBefore;
my %allegCountAfter;

my $defaultAllegiance = 'NaFr';
my @StImWorlds = qw/0137 0237 0338 0340/;
my @ZiSiWorlds = qw/0131 0132 0134 0234 0333 0431 0434 0435 0531 0532 0533 0534 
                    0631 0633 0635 0732 0734 0831 0832 0833 0834 0835
                    0933 0934 1032 1033 1131 1134 1135 1136 1232 1235 1236 1332 1334 1431 1432 1435
                    1531 1533 1534 1535 1632 1633 1634
                    0531 0532 0631 0732 0831 0832 0833
                    2531 2533 2534 2536 2631 2634 2732 2733 2734 2832 2931 2932 2933 2934
                    3031 3032 3131 3134 3231 3232/;

open my $in, '<', '1120_Vlan.sec';
my $hdr1 = <$in>;
my $hdr2 = <$in>;

print $hdr1, $hdr2;
foreach my $line (<$in>)
{
    my ($hex) = split /\s/, $line;
    my ($col,$row) = $hex =~ /(..)(..)/;

    my ($alleg) = substr($line, 101, 4); # =~ /(NaFr|ZiSi|StIm|CsIm|LiIm|NaVa|V17D|VNgC|CRVi)/;
    $allegCountBefore{$alleg}++ if $alleg;
    $allegs{ $alleg }++;

    if ( $row > 30 && $line =~ m/ZiSi|StIm/)
    {
        $line =~ s/ ZiSi | StIm / NaFr /;  # default
        if (grep( /$hex/, @StImWorlds ))
        {
            $line =~ s/ NaFr / StIm /;
        }
        elsif (grep( /$hex/, @ZiSiWorlds ))
        {
            $line =~ s/ NaFr / ZiSi /;
        }
    }
    print $line;

    $alleg = substr($line, 101, 4); # =~ /(NaFr|ZiSi|StIm|CsIm|LiIm|NaVa|V17D|VNgC|CRVi)/;
    $allegCountAfter{$alleg}++ if $alleg;
    $allegs{ $alleg }++;
}
close $in;

printf "Allegiance  # Before  # After\n";
printf "----------  --------  -------\n";
foreach (sort keys %allegs)
{
    printf "%-10s %9d %8d\n", $_, $allegCountBefore{$_}, $allegCountAfter{$_};
}