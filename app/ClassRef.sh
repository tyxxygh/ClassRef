#!/bin/bash

#srcModules=(Engine)
#dstModules=(Renderer)
#srcModules=(Engine)
#dstModules=(RenderCore)
#
#srcModules=(Renderer)
#dstModules=(Engine)
#srcModules=(Renderer)
#dstModules=(RenderCore)
#
#srcModules=(RenderCore)
#dstModules=(Engine)
#srcModules=(RenderCore)
#dstModules=(Renderer)

srcModules=(Engine, CoreUObject, Launch)
dstModules=(Renderer, RenderCore)
#srcModules=(Renderer, RenderCore)
#dstModules=(VulkanRHI)

showInternal=""

##//////
#srcModules=(RenderCore)
#dstModules=(Renderer)
#dstModules=(Engine/Public/MaterialShared.h)
#dstModules=(VulkanRHI)
#srcModules=(Renderer, RenderCore)
#dstModules=(Engine, Launch)

exclusive="NoExportTypes.h"

outputSrc=""
outputDst=""

baseDir="F:\\yao\\vulkanengine\\Engine\\Source\\Runtime\\"

srcPaths=""
for Module in ${srcModules[@]}
do
	srcPaths+=${baseDir}${Module}
	outputSrc+=${Module}
done

dstPaths=""
for Module in ${dstModules[@]}
do
	dstPaths+=${baseDir}${Module}
	outputDst+=${Module}
done

if [ $# -ge 1 ]; then
	showInternal="-i"
	tmp=${srcPaths}
	srcPaths=${dstPaths}
	dstPaths=${tmp}
fi

echo srcPaths $srcPaths
echo dstPaths $dstPaths
output=${outputSrc}_refBy_${outputDst}${showInternal}.txt

ClassRef -m ${srcPaths} -r ${dstPaths} ${showInternal} -s "WITH_EDITOR,0,LOGTRACE_ENABLED,WITH_EDITOR_ONLY_DATA,UE_TRACE_ENABLED,!UE_BUILD_SHIPPING,VULKAN_HAS_DEBUGGING_ENABLED,RDG_ENABLE_DEBUG,WANTS_DRAW_MESH_EVENTS,!USE_BOOT_PROFILING,!(UE_BUILD_SHIPPING,!UE_BUILD_SHIPPING,STATICMESH_ENABLE_DEBUG_RENDERING,WANTS_DRAW_MESH_EVENTS,WITH_SERVER_CODE,INCLUDE_CHAOS,ENABLE_RHI_VALIDATION,VULKAN_MEMORY_TRACK,VULKAN_USE_LLM,VULKAN_OBJECT_TRACKING,VULKAN_CUSTOM_MEMORY_MANAGER_ENABLED" -e linux,unix,${exclusive} > ${output}
#echo $result
#if [ -z "$result" ]; then
#	#echo $currentModule '==>' $userModule
#	continue
#else
#	#echo $currentModule '==>' $userModule
#	#echo "=============== module " $currentModule 'referenced by' $userModule ============== >> result.txt
#	echo $result > result.txt
#	#echo "-----------------------------------" >> result.txt
#	#echo "" >> result.txt
#fi


