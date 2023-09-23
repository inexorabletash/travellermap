foreach my $file (<*.*>)
{
   open IN, $file;
   my @lines = <IN>;
   close IN;

   foreach my $line (@lines)
   {
      my $i = 0;
      foreach (split '', $line)
      {
         ++$i;
         next unless ord($_) == 160;
         print "found nbsp in $file column $i\n";
         print $line;
      }
   }
}
