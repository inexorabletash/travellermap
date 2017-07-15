#!/usr/bin/env perl
use strict;
use warnings;

use File::Spec;
use FindBin;
use lib $FindBin::Bin;

use parseutil;

my $INPUT_LINE_ENDINGS = "\r";
my $INPUT_ENCODING = "UTF-8";

my $input_path = File::Spec->catfile($FindBin::Bin, 'allegiance_codes.tsv');
my @lines;
{
    local $/ = $INPUT_LINE_ENDINGS;
    open my $fh, "<:encoding($INPUT_ENCODING)", $input_path or die;
    my $line;

    $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^ALLEGIANCES$/;
    $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^$/;
    $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^Code\t/;

    while (<$fh>) {
        chomp;
        next if /^\s+$/;
        die "Unexpected: $_\n" unless m/^([A-Za-z0-9']{4}) *\t/;
        my ($alleg, $legacy, $base, $desc, $location) = map { trim($_) } split(/\t/);

        my $comment;
        if ($desc =~ /^([^(]+) (\(\D[^)]+\))$/ ||
            $desc =~ /^([^(]+)(?:, (independent))$/ ||
            $desc =~ /^([^(]+)(?:, (undetermined))$/) {
            $desc = $1;
            $comment = $2;
        }

        $legacy =~ s|/.*||g;

        $alleg = quote($alleg);
        $legacy = quote($legacy);
        $base = $base ? quote($base) : 'null';
        $desc = quote($desc);
        $location = quote($location);

        my $line = "            { $alleg, $legacy, $base, $desc, $location },";
        $line .= " // $comment" if $comment;

        push @lines, $line;
    }
    close $fh;
}

@lines = sort { lc $a cmp lc $b } @lines;

my $replace = join("\n", @lines);

my $code_path = File::Spec->catfile($FindBin::Bin, '..', '..', 'server', 'SecondSurvey.cs');
my $code;
{
    open my $fh, '<:encoding(UTF-8)', $code_path or die;
    local $/ = undef;
    $code = <$fh>;
    close $fh;
}

$code =~ s/(\/\/ Allegiance Table Begin\s*\n)(.*?)(\n\s*\/\/ Allegiance Table End)/$1$replace$3/s;

{
    open my $fh, '>:encoding(UTF-8)', $code_path or die;
    print $fh $code;
    close $fh;
}
