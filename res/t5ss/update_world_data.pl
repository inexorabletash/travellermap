#!/usr/bin/env perl
use strict;
use FileHandle;
use File::Basename;

my $dir = dirname($0);

my %sectors = (
    Akti => "Aktifao",
    Alde => 'Aldebaran',
    Alph => "Alpha Crucis",
    Amdu => 'Amdukan',
    Anta => "Antares",
    Beyo => "Beyond",
    Cano => 'Canopus',
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
    Lang => 'Langere',
    Ley  => "Ley",
    Lish => "Lishun",
    Magy => "Magyar",
    Mass => "Massilia",
    Mend => 'Mendan',
    Mesh => 'Meshan',
    Newo => 'Neworld',
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
    Tien => "Tienspevnekr",
    Troj => "Trojan Reach",
    Tugl => 'Tuglikki',
    Uist => "Uistilrao",
    Ustr => "Ustral Quadrant",
    Vang => "Vanguard Reaches",
    Verg => "Verge",
    Vlan => "Vland",
    Wind => 'Windhorn',
    Zaru => "Zarushagar",
    Ziaf => "Ziafrplians",
    );

# If saving as tab-delimited text from MacOS Excel:
#   Line endings: CR
#   Input encoding: MacRoman
# If copy/pbpaste from MacOS Excel:
#   Line endings: CR
#   Input encoding: UTF-8

my $INPUT_LINE_ENDINGS = "\r";
my $INPUT_ENCODING = "UTF-8";

my %files;
my @lines = ();
my $header;
{
    my @in_files = ('t5ss-im.tsv', 't5ss-nonim.tsv');
    local $/ = $INPUT_LINE_ENDINGS;
    foreach my $file (@in_files) {
        my $count = 0;
        print "processing: $file\n";
        my $in_path = $dir . '/' . $file;
        open my $in, "<:encoding($INPUT_ENCODING)", $in_path or die;
        my $keep = 0;
        foreach my $line (<$in>) {
            chomp $line;
            my $sector = (split('\t', $line))[1];
            if (!$keep && $sector eq 'Sector') {
                $header = $line;
                $keep = 1;
                next;
            }
            next unless $keep && $sector =~ /^....?$/;
            push @lines, $line;
            ++$count;
        }
        print " count: $count lines\n";
    }
}

@lines = sort @lines;

my @header = map { trim($_) } split('\t', $header);

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

my $outdir = $dir . '/../Sectors/M1105';

foreach my $line (@lines) {
    chomp $line;
    my @cols = map { trim($_) } split("\t", $line);
    my %fields = ();
    for my $i (0..$#header) {
        my $value = $cols[$i];
        $value = $1 if $value =~ /^"(.*)"$/;
        $fields{$header[$i]} = $value;
    }
    my $sec = $fields{'Sector'};
    next if $sec eq 'ZZZZ'; # Sentinel

    if (!exists $files{$sec}) {
        die "Unknown sector code: $sec\n" unless exists $sectors{$sec};
        print "Processing $sec...\n";
        my $fh = FileHandle->new;
        $files{$sec} = $fh;
        open ($fh, '>:encoding(UTF-8)', "$outdir/$sectors{$sec}.tab");
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
