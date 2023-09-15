	DEVICE ZXSPECTRUM48

	org 32768
begin:
	di	
	ld sp,32767 : 	xor a :  out (254),a
main:

	ei : halt
	ld hl,module : call vktInit

mainLoop:	
	halt	
	and 7 : out (254),a	
	call vktPlay

	jp mainLoop

module:      
	incbin "music\music.vtk"     

	include "playerByte.a80"

	savetap "main.tap",begin

                                                                                                        

