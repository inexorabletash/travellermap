#!/usr/bin/env perl
use strict;
use File::Basename;

my $dir = dirname($0);

my $code_path = $dir . '/../../server/SecondSurvey.cs';
my $code;
{
    open my $fh, '<', $code_path or die;
    local $/ = undef;
    $code = <$fh>;
    close $fh;
}

sub quote($) {
    my ($s) = @_;
    $s =~ s/["\\]/\\$1/g;
    return '"' . $s . '"';
}

my $alleg_path = $dir . '/allegiance_codes.tsv';
my @lines;
{
    open my $fh, '<', $alleg_path or die;
    while (<$fh>) {
        chomp;
        next unless m/^(\w\w\w\w)\t/;
        next if $1 eq "Code";
        my ($alleg, $legacy, $base, $desc, $location) = split(/\t/);

        my $comment;
        if ($desc =~ /^([^(]+) (\(\D[^)]+\))$/ ||
            $desc =~ /^([^(]+)(?:, (independent))$/ ||
            $desc =~ /^([^(]+)(?:, (undetermined))$/) {
            $desc = $1;
            $comment = $2;
        }

        $alleg = quote($alleg);
        $legacy = quote($legacy);
        $base = $base ? quote($base) : 'null';
        $desc = quote($desc);

        my $line = "            { $alleg, $legacy, $base, $desc },";
        $line .= " // $comment" if $comment;

        push @lines, $line;
    }
    close $fh;
}

@lines = sort @lines;

my $replace = join("\n", @lines);

$code =~ s/(\/\/ Allegiance Table Begin\s*\n)(.*?)(\n\s*\/\/ Allegiance Table End)/$1$replace$3/s;

{
    open my $fh, '>', $code_path or die;
    print $fh $code;
    close $fh;
}
