# ClassRef
ClassRef finds references of classes(including class/struct/enum/globalFunction/globalVariable) defined in some dirs and used in someother dirs for C++ project.

# Usage:
ClassRef.exe -m moduleDir -r referenceModuleDir [option]
	
	Required:
	-m --modules   : folders where classes are defined.
	-r --ref-modules : folders where classes are used.

	Options:
	-e --exclusive : exclude some specify directory, case insensitive.
	-s --skipMacro £ºskip codes surround by #if XXX #endif, case insensitive.
	-i --internal  : also shows internal classes(classes not referenced).
	-v --verbos    : show verbos result.
	-d --debug     : debug this tool to see if it counts like what you'v expected.
