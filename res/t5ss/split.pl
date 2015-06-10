#!/usr/bin/env perl
use strict;
use FileHandle;

my %sectors = (
    Akti => "Aktifao",
    Alph => "Alpha Crucis",
    Anta => "Antares",
    Core => "Core",
    Corr => "Corridor",
    Dagu => "Dagudashaag",
    Daib => "Daibei",
    Dark => "Dark Nebula",
    Delp => "Delphi",
    Dene => "Deneb",
    Dias => "Diaspora",
    Eali => "Ealiyasiyw",
    Empt => "Empty Quarter",
    Fore => "Foreven",
    Forn => "Fornast",
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
    Olde => "Old Expanses",
    Reav => "Reavers Deep",
    Reft => "Reft",
    Rift => "Riftspan Reaches",
    Solo => "Solomani Rim",
    Spic => "Spica",
    Spin => "Spinward Marches",
    Stai => "Staihaia'yo",
    Troj => "Trojan Reach",
    Uist => "Uistilrao",
    Ustr => "Ustral Quadrant",
    Verg => "Verge",
    Vlan => "Vland",
    Zaru => "Zarushagar",
    Ziaf => "Ziafrplians",
    );

my %files;

my $line = <STDIN>;
chomp $line;
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

foreach $line (<STDIN>) {
    chomp $line;
    my @cols = map { trim($_) } split("\t", $line);
    my %fields = ();
    for my $i (0..$#header) {
        $fields{$header[$i]} = $cols[$i];
    }
    my $sec = $fields{'Sector'};
    if (!exists $files{$sec}) {
        $files{$sec} = FileHandle->new("> $sectors{$sec}.tab");
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
