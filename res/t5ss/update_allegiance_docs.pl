#!/usr/bin/env perl
use strict;
use File::Basename;

my $dir = dirname($0);

my $html_path = $dir . '/../../doc/secondsurvey.html';
my $html;
{
    open my $fh, '<', $html_path or die;
    local $/ = undef;
    $html = <$fh>;
    close $fh;
}


my $alleg_path = $dir . '/allegiance_codes.tsv';
my @lines;
{
    open my $fh, '<', $alleg_path or die;
    while (<$fh>) {
        chomp;
        next unless m/^(\w\w\w\w)\t/;
        next if $1 eq "Code";
        my ($code, $legacy, $base, $name, $location) = split(/\t/);
        push @lines, "      <tr><td><code>$code</code><td>$name";
    }
    close $fh;
}

@lines = sort @lines;

my $replace = join("\n", @lines);

$html =~ s/(<!-- Allegiance Table Begin -->\s*\n)(.*?)(\n\s*<!-- Allegiance Table End -->)/$1$replace$3/s;

{
    open my $fh, '>', $html_path or die;
    print $fh $html;
    close $fh;
}
