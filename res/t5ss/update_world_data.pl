#!/usr/bin/env perl
use strict;
use warnings;

use File::Spec;
use FileHandle;
use FindBin;
use lib $FindBin::Bin;

use parseutil;

my %sectors = (
    Afaw => "Afawahisa",
    Akti => "Aktifao",
    Alde => 'Aldebaran',
    Alph => "Alpha Crucis",
    Amdu => 'Amdukan',
    Anta => "Antares",
    Arzu => "Arzul",
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
    Gash => "Gashikan",
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
    Star => "Star's End",
    Thet => "Theta Borealis",
    Tien => "Tienspevnekr",
    Touc => "Touchstone",
    Tren => "Trenchans",
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



#
# Parse T5SS TSV files
#
my @lines = ();
my $header;
{
    my @in_files = ('t5ss-im.tsv', 't5ss-nonim.tsv');
    local $/ = $INPUT_LINE_ENDINGS;
    foreach my $file (@in_files) {
        my $count = 0;
        print "Processing: $file\n";
        my $in_path = File::Spec->catfile($FindBin::Bin, $file);
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
            next unless $keep && $sector && $sector =~ /^....?$/;
            push @lines, $line;
            ++$count;
        }
        print " count: $count lines\n";
    }
}

#
# Parse anomalies (calibration points, etc) column-delimited file
#
my @anomalies;
{
    my $in_path = File::Spec->catfile($FindBin::Bin, 'anomalies.txt');
    open my $in, "<:encoding($INPUT_ENCODING)", $in_path or die;
    local $/ = undef;
    my $data = <$in>;
    close $in;
    @anomalies = parseColumnDelimited($data);

    print "Anomalies: ", scalar(@anomalies), "\n"
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


#
# Start outputting sector files
#

my $outdir = File::Spec->catdir($FindBin::Bin, '..', 'Sectors', 'M1105');
my %files;

# Look up appropriate sector file handle, open if needed
sub fileForSector($) {
    my ($sec) = @_;
    return $files{$sec} if exists $files{$sec};
    die "Unknown sector code: $sec\n" unless exists $sectors{$sec};

    # Indicate progress
    print "Processing $sec...\n";

    my $fh = FileHandle->new;
    $files{$sec} = $fh;
    open ($fh, '>:encoding(UTF-8)', File::Spec->catfile($outdir, "$sectors{$sec}.tab"));
    print { $fh } join("\t", @outheader), "\n";
    return $fh;
}


foreach my $line (@lines) {
    # Parse tab-delimited lines into fields
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

    # Synthesize fields
    $fields{'SS'} = hexToSS($fields{'Hex'});
    $fields{'Remarks'} = combine($fields{'TC'}, $fields{'Remarks'}, $fields{'Sophonts'}, $fields{'Details'});
    $fields{'Name'} = $fields{'M1000 Names'};

    # Write fields, per output header
    my @out;
    for my $i (0..$#outheader) {
        $out[$i] = $fields{$outheader[$i]}
    }

    my $fh = fileForSector($sec);
    print { $fh } join("\t", @out), "\n";
}

# Append anomaly entries

print "Processing Anomalies...\n";
foreach my $anomaly (@anomalies) {
    my $sec = $anomaly->{'Sector'};

    # Synthesize fields
    $anomaly->{'SS'} = hexToSS($anomaly->{'Hex'});

    # Write fields, per output header
    my @out;
    for my $i (0..$#outheader) {
        $out[$i] = $anomaly->{$outheader[$i]} // '';
    }

    my $fh = fileForSector($sec);
    print { $fh } join("\t", @out), "\n";
}
