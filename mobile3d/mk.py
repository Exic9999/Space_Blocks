import sys
src=open('index.html').read().replace('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js','three.min.js')
pre=sys.argv[2]
test='<script>\nwindow.addEventListener("error",e=>{document.title="ERR "+e.message+" @"+e.lineno});\nsetTimeout(()=>{try{'+pre+'}catch(e){document.title="ERR "+e.stack.slice(0,300)}},300);\n</script>'
open(sys.argv[1],'w').write(src+test)
