#!python

import optparse
import glob
import re

def check_file(filename):
	freq = {}
	for line in file(filename):
		line = line.rstrip()
		if len(line) > 0 and line[0] not in ["#", "$", "@", " ", "-", ".", "*"]:
			m = re.match(r"^(.{5,40}?) +(\d\d\d\d) +(\w\w\w\w\w\w\w-\w) ", line)
			if m:
				(name, hex, uwp) = ( m.group(1), m.group(2), m.group(3) )

				freq[name+uwp] = freq.get(name+uwp,0) + 1

	ndupes = 0

	for key in freq:
		if freq[key] > 1:
			ndupes += freq[key]

	print ndupes, filename

	for key in freq:
		if freq[key] > 1:
			print "    ", key, freq[key]


if __name__ == "__main__":
	parser = optparse.OptionParser()
	(options, args) = parser.parse_args()

	for arg in args:
		for filename in glob.glob(arg):
			check_file(filename)
