export type FileType = "image"|"video"|"audio"|"text"|"unknown";

export function getFileType(fileName:string):FileType{
    if(imageFileExts.find(x=>fileName.endsWith(x))){
        return "image"
    }
    if(videoFileExts.find(x=>fileName.endsWith(x))){
        return "video"
    }
    if(textFileExts.find(x=>fileName.endsWith(x))){
        return "text"
    }
    if(audioFileExts.find(x=>fileName.endsWith(x))){
        return "audio"
    }
    return "unknown"
}

export const imageFileExts=[
    '.png',
    '.jpg','.jpeg',
    '.gif','.webp',
    '.svg','.svgz',
    '.tif','.tiff','.pjp','.pjpeg',
    '.xbm','.ico','.jfif','.avif'
]
export const videoFileExts=[
    '.mp4','.webm'
]
export const textFileExts=[
    '.txt','.md','.json','.xml',
    '.html','.js','.css'
]
export const audioFileExts=[
    '.mp3','.ogg','.aac'
]

export function fileSizeStr(bytes:number){
    if(bytes<1000){
        return bytes+"B";
    }
    if(bytes<1000*1000){
        return Math.round(bytes/1000)+"K";
    }
    return (Math.round(bytes/(1000*100)))*10+"M"
}