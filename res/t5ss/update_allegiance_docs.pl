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

sub trim ($) {
    my ($s) = @_;
    $s =~ s/^\s+//;
    $s =~ s/\s+$//;
    return $s;
}

my $alleg_path = $dir . '/allegiance_codes.tsv';
my @lines;
{
    open my $fh, '<', $alleg_path or die;

    my $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^ALLEGIANCES$/;
    my $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^$/;
    my $line = <$fh>; chomp $line; $line = trim($line);
    die "Unexpected header: $line\n" unless $line =~ /^Code\t/;

    while (<$fh>) {
        chomp;
        die "Unexpected: $_\n" unless m/^([A-Za-z0-9']{4}) *\t/;
        my ($code, $legacy, $base, $name, $location) = map { trim($_) } split(/\t/);
        $location =~ s|/|/&#x200B;|g;
        push @lines, "      <tr><td><code>$code</code><td>$name<td>$location";
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
