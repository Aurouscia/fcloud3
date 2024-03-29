import { Router } from "vue-router";
import { addToRouter } from "../../utils/routerAdd";
import FileDirIndex from './FileDirIndex.vue'

export function addFiles(r:Router){
    addToRouter(r,routes);
}

const routes = [
    {
        name: 'files',
        path: '/d/:path(.*)*',
        component: FileDirIndex,
        props:true
    }
]