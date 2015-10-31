#!/usr/bin/env perl
use strict;
use FileHandle;
use File::Basename;

my %sectors = (
    Akti => "Aktifao",
    Alph => "Alpha Crucis",
    Amdu => 'Amdukan',
    Anta => "Antares",
    Beyo => "Beyond",
    Core => "Core",
    Corr => "Corridor",
    Cruc => "Crucis Margin",
    Dagu => "Dagudashaag",
    Daib => "Daibei",
    Dark => "Dark Nebula",
    Delp => "Delphi",
    Dene => "Deneb",
    Dias => "Diaspora",
    Eali => "Ealiyasiyw",
    Empt => "Empty Quarter",
    Farf => "Far Frontiers",
    Fore => "Foreven",
    Forn => "Fornast",
    Gate => "Gateway",
    Glim => "Glimmerdrift",
    Gush => "Gushemege",
    Gvur => "Gvurrdon",
    Hint => "Hinterworlds",
    Hlak => "Hlakhoi",
    Ilel => "Ilelish",
    Iwah => "Iwahfuah",
    Ley  => "Ley",
    Lish => "Lishun",
    Magy => "Magyar",
    Mass => "Massilia",
    Mend => 'Mendan',
    Mesh => 'Meshan',
    Olde => "Old Expanses",
    Prov => 'Provence',
    Reav => "Reavers Deep",
    Reft => "Reft",
    Rift => "Riftspan Reaches",
    Solo => "Solomani Rim",
    Spic => "Spica",
    Spin => "Spinward Marches",
    Stai => "Staihaia'yo",
    Thet => "Theta Borealis",
    Troj => "Trojan Reach",
    Tugl => 'Tugliki',
    Uist => "Uistilrao",
    Ustr => "Ustral Quadrant",
    Vang => "Vanguard Reaches",
    Verg => "Verge",
    Vlan => "Vland",
    Wind => 'Windhorn',
    Zaru => "Zarushagar",
    Ziaf => "Ziafrplians",
    );

my %files;

my $dir = dirname($0);

my $in_path = $dir . '/world_data.tsv';
open my $in, '<', $in_path or die;
my $line;

$line = <$in>; chomp $line; $line =~ s/\s+$//;
die "Unexpected header: $line\n" unless $line =~ /^WORLD DATA$/;

$line = <$in>; chomp $line; $line =~ s/\s+$//;
die "Unexpected header: $line\n" unless $line =~ /^$/;

$line = <$in>; chomp $line;
die "Unexpected header: $line\n" unless $line =~ /^Sector\tHex\tName\tUWP\t/;

my @header = map { trim($_) } split('\t', $line);

my @outheader = (
    'Sector',
    'SS',
    'Hex',
    'Name',
    'UWP',
    'Bases',
    'Remarks',
    'Zone',
    'PBG',
    'Allegiance',
    'Stars',
    '{Ix}',
    '(Ex)',
    '[Cx]',
    'Nobility',
    'W',
    'RU'
    );

#Sector Hex Name UWP TC Remarks Sophonts Details {Ix} (Ex) [Cx] Nobility Bases Zone PBG Allegiance Stars W RU

sub trim ($) {
    my ($s) = @_;
    $s =~ s/^\s+//;
    $s =~ s/\s+$//;
    return $s;
}

sub combine {
    my $result = '';
    while (@_) {
        my $f = shift @_;
        next if $f eq '';
        $result .= ' ' if $result ne '';
        $result .= $f;
    }
    return $result;
}

sub hexToSS {
    my ($hex) = @_;
    my $x = int($hex / 100);
    my $y = $hex % 100;
    my $ssx = int(($x-1) / 8);
    my $ssy = int(($y-1) / 10);
    return chr(ord('A') + $ssx + $ssy * 4);
}

foreach $line (<$in>) {
    chomp $line;
    my @cols = map { trim($_) } split("\t", $line);
    my %fields = ();
    for my $i (0..$#header) {
        $fields{$header[$i]} = $cols[$i];
    }
    my $sec = $fields{'Sector'};
    if (!exists $files{$sec}) {
        die "Unknown sector code: $sec\n" unless exists $sectors{$sec};
        $files{$sec} = FileHandle->new("> $dir/../Sectors/$sectors{$sec}.tab");
        print { $files{$sec} } join("\t", @outheader), "\n";
    }

    $fields{'SS'} = hexToSS($fields{'Hex'});

    $fields{'Remarks'} = combine($fields{'TC'}, $fields{'Remarks'}, $fields{'Sophonts'}, $fields{'Details'});


    my @out;
    for my $i (0..$#outheader) {
        $out[$i] = $fields{$outheader[$i]}
    }
    print { $files{$sec} } join("\t", @out), "\n";
}

close $in;
