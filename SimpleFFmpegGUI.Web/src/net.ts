import Vue from "vue";
import { AxiosResponse } from "axios";


function getUrl(controller: string): string {
        return `https://localhost:44305/${controller}`;
}

export function postResetTask(id: number): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Reset?id=" + id));
}

export function postResetTasks(ids: number[]): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Reset/List"), ids);
}

export function postCancelTasks(ids: number[]): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Cancel/List"), ids);
}

export function postDeleteTasks(ids: number[]): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Delete/List"), ids);
}
export function postCancelTask(id: number): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Cancel?id=" + id));
}
export function postCancelQueue(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Queue/Cancel"));
}
export function postStartQueue(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Queue/Start"));
}
export function postAddCodeTask(item: any): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Task/Add/Code"), item);
}
export function getQueueStatus(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .get(getUrl("Queue/Status"))
}
export function getTaskList(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .get(getUrl("Task/List"))
}

export function getMediaInfo(name: string): Promise<AxiosResponse<any>> {
        return Vue.axios
                .get(getUrl("MediaInfo") + "?name=" + name)
}

export function getMediaNames(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .get(getUrl("MediaDir"))
}

export function getPresets(): Promise<AxiosResponse<any>> {
        return Vue.axios
                .get(getUrl("Preset/List"))
}
export function postAddOrUpdatePreset(item: any): Promise<AxiosResponse<any>> {
        return Vue.axios
                .post(getUrl("Preset/Add"), item);
}