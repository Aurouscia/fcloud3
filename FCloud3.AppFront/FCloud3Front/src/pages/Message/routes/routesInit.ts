import { Router } from "vue-router";
import Notifications from "../Notifications.vue";
import { addToRouter } from "@/utils/routerAdd";

export function addMessages(r:Router){
    addToRouter(r,routes);
}

const routes = [
    {
        path:"/notifications",
        component: Notifications,
        name:"notifs"
    }
]